using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UAssetAPI;

namespace AstroModIntegrator
{
    public static class PakBaker
    {
        public static byte[] Bake(Dictionary<string, byte[]> data)
        {
            MemoryStream ourStream = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(ourStream);

            // Initial Record blocks
            Dictionary<Record, byte[]> records = new Dictionary<Record, byte[]>();
            foreach (KeyValuePair<string, byte[]> entry in data)
            {
                var rec = new Record { offset = (ulong)writer.BaseStream.Position, fileName = entry.Key };
                rec.Write(writer, entry.Value, false);
                writer.Write(entry.Value);
                records.Add(rec, entry.Value);
            }

            // Mount point and record count
            ulong indexOffset = (ulong)writer.BaseStream.Position;
            writer.WriteUString("../../../");
            writer.Write((int)data.Count);

            // Second Record blocks
            foreach (KeyValuePair<Record, byte[]> entry in records)
            {
                entry.Key.Write(writer, entry.Value, true);
            }

            // Footer
            ulong indexEndOffset = (ulong)writer.BaseStream.Position;
            ulong indexLength = indexEndOffset - indexOffset;
            writer.Write((byte)0);
            writer.Write(0x5A6F12E1); // magic number
            writer.Write((int)4); // type 4
            writer.Write(indexOffset);
            writer.Write(indexLength);

            ulong shaOffset = (ulong)writer.BaseStream.Position;
            writer.Seek((int)indexOffset, SeekOrigin.Begin);
            byte[] indexData = new byte[indexLength];
            writer.BaseStream.Read(indexData, 0, (int)indexLength);

            writer.Seek((int)shaOffset, SeekOrigin.Begin);
            writer.Write(new SHA1Managed().ComputeHash(indexData));

            return ourStream.ToArray();
        }
    }
}
