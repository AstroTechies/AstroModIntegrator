using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UAssetAPI;
using UAssetAPI.PropertyTypes;
using UAssetAPI.StructTypes;

namespace AstroModIntegrator
{
    public class DataTableBaker
    {
        public DataTableBaker()
        {

        }

        public MemoryStream Bake(Metadata[] allMods, byte[] superRawData)
        {
            BinaryReader yReader = new BinaryReader(new MemoryStream(superRawData));
            AssetWriter y = new AssetWriter
            {
                WillStoreOriginalCopyInMemory = true,
                WillWriteSectionSix = true,
                data = new AssetReader()
            };
            y.data.Read(yReader);
            y.OriginalCopy = superRawData;

            DataTableCategory targetCategory = null;
            foreach (Category cat in y.data.categories)
            {
                if (cat is DataTableCategory)
                {
                    targetCategory = (DataTableCategory)cat;
                    break;
                }
            }

            List<DataTableEntry> tab = targetCategory.Data2.Table;
            string[] columns = tab[0].Data.Value.Select(x => x.Name).ToArray();

            Dictionary<string, int> DuplicateIndexLookup = new Dictionary<string, int>();
            List<DataTableEntry> newTable = new List<DataTableEntry>();
            foreach (Metadata mod in allMods)
            {
                if (mod == null) continue;

                Dictionary<int, string> rows = new Dictionary<int, string>();
                rows[0] = mod.Name;
                rows[1] = mod.Author;
                rows[2] = mod.Description;
                rows[3] = mod.ModVersion?.ToString();
                rows[4] = mod.AstroBuild?.ToString();
                rows[5] = mod.Sync.GetEnumMemberAttrValue();
                rows[6] = mod.Homepage;

                y.data.AddHeaderReference(mod.ModID);
                foreach (KeyValuePair<int, string> entry in rows)
                {
                    if (entry.Value == null) continue;
                    y.data.AddHeaderReference(entry.Value);
                }

                if (!DuplicateIndexLookup.ContainsKey(mod.ModID)) DuplicateIndexLookup[mod.ModID] = 0;
                newTable.Add(new DataTableEntry(new StructPropertyData(mod.ModID, y.data)
                {
                    StructType = tab[0].Data.StructType,
                    Value = rows.Select(x => (PropertyData)new StrPropertyData(columns[x.Key], y.data)
                    {
                        Value = x.Value ?? "",
                        Encoding = Encoding.ASCII
                    }).ToList()
                }, DuplicateIndexLookup[mod.ModID]));
                DuplicateIndexLookup[mod.ModID]++;
            }

            targetCategory.Data2.Table = newTable;
            return y.WriteData(new BinaryReader(new MemoryStream(y.OriginalCopy)));
        }
    }
}
