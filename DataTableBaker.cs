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

        public MemoryStream Bake(Metadata[] allMods, List<string> optionalModIDs, byte[] superRawData)
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

                y.data.AddHeaderReference(mod.ModID);

                string codedSyncMode = "SyncMode::NewEnumerator0";
                switch(mod.Sync)
                {
                    case SyncMode.None:
                        codedSyncMode = "SyncMode::NewEnumerator0";
                        break;
                    case SyncMode.ServerOnly:
                        codedSyncMode = "SyncMode::NewEnumerator1";
                        break;
                    case SyncMode.ClientOnly:
                        codedSyncMode = "SyncMode::NewEnumerator2";
                        break;
                    case SyncMode.ServerAndClient:
                        codedSyncMode = "SyncMode::NewEnumerator3";
                        break;
                }

                List<PropertyData> rows = new List<PropertyData>();
                rows.Add(new StrPropertyData(columns[0], y.data)
                {
                    Value = mod.Name ?? "",
                    Encoding = Encoding.ASCII
                });
                rows.Add(new StrPropertyData(columns[1], y.data)
                {
                    Value = mod.Author ?? "",
                    Encoding = Encoding.ASCII
                });
                rows.Add(new StrPropertyData(columns[2], y.data)
                {
                    Value = mod.Description ?? "",
                    Encoding = Encoding.ASCII
                });
                rows.Add(new StrPropertyData(columns[3], y.data)
                {
                    Value = mod.ModVersion?.ToString() ?? "",
                    Encoding = Encoding.ASCII
                });
                rows.Add(new StrPropertyData(columns[4], y.data)
                {
                    Value = mod.AstroBuild?.ToString() ?? "",
                    Encoding = Encoding.ASCII
                });
                rows.Add(new BytePropertyData(columns[5], y.data)
                {
                    ByteType = BytePropertyType.Long,
                    EnumType = y.data.AddHeaderReference("SyncMode"),
                    Value = y.data.AddHeaderReference(codedSyncMode)
                });
                rows.Add(new StrPropertyData(columns[6], y.data)
                {
                    Value = mod.Homepage ?? "",
                    Encoding = Encoding.ASCII
                });
                rows.Add(new BoolPropertyData(columns[7], y.data)
                {
                    Value = (mod.Sync != SyncMode.ServerAndClient) || optionalModIDs.Contains(mod.ModID),
                });

                if (!DuplicateIndexLookup.ContainsKey(mod.ModID)) DuplicateIndexLookup[mod.ModID] = 0;
                newTable.Add(new DataTableEntry(new StructPropertyData(mod.ModID, y.data)
                {
                    StructType = tab[0].Data.StructType,
                    Value = rows
                }, DuplicateIndexLookup[mod.ModID]));
                DuplicateIndexLookup[mod.ModID]++;
            }

            targetCategory.Data2.Table = newTable;
            return y.WriteData(new BinaryReader(new MemoryStream(y.OriginalCopy)));
        }
    }
}
