using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using UAssetAPI;

namespace AstroModIntegrator
{
    public struct Block
    {
        public ulong Start;
        public ulong Size;

        public Block(ulong start, ulong size)
        {
            Start = start;
            Size = size;
        }
    }

    public class Record
    {
        public string fileName;
        public ulong offset;
        public ulong fileSize;
        public ulong sizeDecompressed;
        public CompressionMethod compressionMethod;
        public bool isEncrypted;
        public uint compressionBlockSize;
        public List<Block> compressionBlocks;

        public void Read(BinaryReader reader, uint fileVersion, bool includesHeader)
        {
            if (includesHeader) fileName = reader.ReadUString();
            offset = reader.ReadUInt64();
            fileSize = reader.ReadUInt64();
            sizeDecompressed = reader.ReadUInt64();
            compressionMethod = (CompressionMethod)reader.ReadUInt32();

            if (fileVersion <= 1)
            {
                ulong timestamp = reader.ReadUInt64();
            }

            reader.ReadBytes(20); // sha1 hash

            if (fileVersion >= 3)
            {
                if (compressionMethod != 0)
                {
                    compressionBlocks = new List<Block>();
                    uint blockCount = reader.ReadUInt32();
                    for (int j = 0; j < blockCount; j++)
                    {
                        ulong startOffset = reader.ReadUInt64();
                        ulong endOffset = reader.ReadUInt64();
                        compressionBlocks.Add(new Block(startOffset, endOffset - startOffset));
                    }
                }

                isEncrypted = reader.ReadByte() > 0;
                compressionBlockSize = reader.ReadUInt32();
            }
        }

        public void Write(BinaryWriter writer, byte[] data, bool includesHeader) // fileVersion is 4
        {
            if (includesHeader) writer.WriteUString(fileName);
            writer.Write(offset);
            writer.Write((ulong)data.Length);
            writer.Write((ulong)data.Length);
            writer.Write((int)CompressionMethod.NONE);
            writer.Write(new SHA1Managed().ComputeHash(data));
            writer.Write((byte)0); // not encrypted
            writer.Write((int)0); // no compressed blocks
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

    public class MetadataExtractor
    {
        internal static uint UE4_PAK_MAGIC = 0x5A6F12E1;
        private uint fileVersion;
        private BinaryReader reader;
        public Dictionary<string, long> PathToOffset;

        public MetadataExtractor(BinaryReader reader)
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
                reader.BaseStream.Seek(-160 - 44, SeekOrigin.End);
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
                        ulong blockOffset = block.Start;
                        ulong blockSize = block.Size;
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

        public Metadata Read()
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
