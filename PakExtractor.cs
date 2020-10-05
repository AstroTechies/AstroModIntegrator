using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using UAssetAPI;

namespace AstroModIntegrator
{
    public struct Block
    {
        public long Start;
        public long Size;

        public Block(long start, long size)
        {
            Start = start;
            Size = size;
        }
    }

    public class Record
    {
        public string fileName;
        public long offset;
        public long fileSize;
        public long sizeDecompressed;
        public CompressionMethod compressionMethod;
        public bool isEncrypted;
        public uint compressionBlockSize;
        public List<Block> compressionBlocks;
        public byte[] dataHash;

        public void Read(BinaryReader reader, uint fileVersion, bool includesHeader)
        {
            if (includesHeader) fileName = reader.ReadUString();
            offset = reader.ReadInt64();
            fileSize = reader.ReadInt64();
            sizeDecompressed = reader.ReadInt64();
            compressionMethod = (CompressionMethod)reader.ReadUInt32();

            if (fileVersion <= 1)
            {
                ulong timestamp = reader.ReadUInt64();
            }

            dataHash = reader.ReadBytes(20); // sha1 hash

            if (fileVersion >= 3)
            {
                if (compressionMethod != CompressionMethod.NONE)
                {
                    compressionBlocks = new List<Block>();
                    uint blockCount = reader.ReadUInt32();
                    for (int j = 0; j < blockCount; j++)
                    {
                        long startOffset = reader.ReadInt64();
                        long endOffset = reader.ReadInt64();
                        compressionBlocks.Add(new Block(startOffset, endOffset - startOffset));
                    }
                }

                isEncrypted = reader.ReadBoolean();
                compressionBlockSize = reader.ReadUInt32(); // max size of each block
            }
        }

        public void Write(BinaryWriter writer, byte[] data, bool includesHeader, bool autoAdjustBlocks, List<Block> blockOffsets = null, byte[] compressedData = null) // fileVersion is 4
        {
            if (autoAdjustBlocks)
            {
                fileSize = compressedData.Length;
                sizeDecompressed = data.Length;
                compressionMethod = CompressionMethod.ZLIB;
                dataHash = new SHA1Managed().ComputeHash(compressedData);
            }

            if (includesHeader) writer.WriteUString(fileName);
            writer.Write(offset);
            writer.Write(fileSize); // normal size
            writer.Write(sizeDecompressed); // decompressed size
            writer.Write((int)compressionMethod);

            writer.Write(dataHash);
            long blockOffsetWritingStart = 0;
            if (autoAdjustBlocks)
            {
                writer.Write(blockOffsets.Count);
                blockOffsetWritingStart = writer.BaseStream.Position;
                foreach (Block b in blockOffsets)
                {
                    writer.Write(b.Start);
                    writer.Write(b.Start + b.Size);
                }
            }
            else
            {
                writer.Write(compressionBlocks.Count);
                foreach (Block b in compressionBlocks)
                {
                    writer.Write(b.Start);
                    writer.Write(b.Start + b.Size);
                }
            }
            writer.Write((byte)0); // not encrypted
            writer.Write((int)sizeDecompressed);

            if (autoAdjustBlocks)
            {
                long endOffset = writer.BaseStream.Position;
                writer.Seek((int)blockOffsetWritingStart, SeekOrigin.Begin);

                compressionBlocks = new List<Block>();
                for (int i = 0; i < blockOffsets.Count; i++)
                {
                    long newStart = endOffset + blockOffsets[i].Start;
                    long newEnd = endOffset + blockOffsets[i].Start + blockOffsets[i].Size;
                    writer.Write(newStart);
                    writer.Write(newEnd);
                    compressionBlocks.Add(new Block(newStart, newEnd - newStart));
                }

                writer.Seek((int)endOffset, SeekOrigin.Begin);
            }
        }

        public Record()
        {

        }
    }

    public enum CompressionMethod
    {
        NONE,
        ZLIB,
        BIAS_MEMORY,
        BIAS_SPEED
    }

    public class InvalidFileTypeException : IOException
    {
        public InvalidFileTypeException(string txt) : base(txt)
        {

        }
    }

    public class PakExtractor
    {
        internal static uint UE4_PAK_MAGIC = 0x5A6F12E1;
        private uint fileVersion;
        private BinaryReader reader;
        public Dictionary<string, long> PathToOffset;

        public PakExtractor(BinaryReader reader)
        {
            this.reader = reader;
            BuildDict();
        }

        private void BuildDict()
        {
            PathToOffset = new Dictionary<string, long>();

            reader.BaseStream.Seek(-44, SeekOrigin.End); // First we head straight to the footer

            uint magic = reader.ReadUInt32();
            if (magic != UE4_PAK_MAGIC) // Magic number
            {
                reader.BaseStream.Seek(-204, SeekOrigin.End);
                magic = reader.ReadUInt32();
                if (magic != UE4_PAK_MAGIC) throw new InvalidFileTypeException("Invalid file format, magic = " + magic);
            }

            fileVersion = reader.ReadUInt32();
            ulong indexOffset = reader.ReadUInt64();
            ulong indexSize = reader.ReadUInt64();

            // First we read the first file record to see if everything is OK
            reader.BaseStream.Seek(0, SeekOrigin.Begin);
            var firstRec = new Record();
            firstRec.Read(reader, fileVersion, false);
            if (firstRec.isEncrypted) throw new NotImplementedException("Encryption is not supported");

            // Start reading the proper index
            reader.BaseStream.Seek((long)indexOffset, SeekOrigin.Begin);
            string mountPoint = reader.ReadUString();
            int recordCount = reader.ReadInt32();

            for (int i = 0; i < recordCount; i++)
            {
                var rec = new Record();
                rec.Read(reader, fileVersion, true);
                PathToOffset.Add(rec.fileName, (long)rec.offset);
            }
        }

        public bool HasPath(string searchPath)
        {
            return PathToOffset.ContainsKey(searchPath);
        }

        public byte[] ReadRaw(string searchPath)
        {
            if (!HasPath(searchPath)) return new byte[0];
            long fullOffset = PathToOffset[searchPath];
            reader.BaseStream.Seek(fullOffset, SeekOrigin.Begin);
            var rec2 = new Record();
            rec2.Read(reader, fileVersion, false);

            switch (rec2.compressionMethod)
            {
                case CompressionMethod.NONE:
                    return reader.ReadBytes((int)rec2.fileSize);
                case CompressionMethod.ZLIB:
                    MemoryStream fullStream = new MemoryStream();
                    foreach (Block block in rec2.compressionBlocks)
                    {
                        long blockOffset = block.Start;
                        long blockSize = block.Size;
                        if (fileVersion == 8) // Relative offset
                        {
                            reader.BaseStream.Seek((long)blockOffset + fullOffset, SeekOrigin.Begin);
                        }
                        else // Absolute offset
                        {
                            reader.BaseStream.Seek((long)blockOffset, SeekOrigin.Begin);
                        }
                        var memStream = new MemoryStream(reader.ReadBytes((int)blockSize));
                        memStream.ReadByte();
                        memStream.ReadByte();
                        using (DeflateStream decompressionStream = new DeflateStream(memStream, CompressionMode.Decompress))
                        {
                            fullStream.Seek(0, SeekOrigin.End);
                            decompressionStream.CopyTo(fullStream);
                        }
                    }

                    return fullStream.ToArray();
                default:
                    throw new NotImplementedException("Unimplemented compression method " + rec2.compressionMethod);
            }
        }

        public Metadata ReadMetadata()
        {
            string data = Encoding.UTF8.GetString(ReadRaw("metadata.json"));
            if (string.IsNullOrEmpty(data)) return null;
            try
            {
                return JsonConvert.DeserializeObject<Metadata>(data);
            }
            catch (JsonReaderException)
            {
                return null;
            }
        }
    }
}
