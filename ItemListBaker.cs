using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UAssetAPI;
using UAssetAPI.PropertyTypes;

namespace AstroModIntegrator
{
    public class ItemListBaker
    {
        PakExtractor Extractor;
        public ItemListBaker(PakExtractor extractor)
        {
            Extractor = extractor;
        }

        public MemoryStream Bake(Dictionary<string, List<string>> newItems, byte[] superRawData)
        {
            BinaryReader yReader = new BinaryReader(new MemoryStream(superRawData));
            AssetWriter y = new AssetWriter
            {
                WillStoreOriginalCopyInMemory = true, WillWriteSectionSix = true, data = new AssetReader()
            };
            y.data.Read(yReader);
            y.OriginalCopy = superRawData;

            // Find some categories
            Dictionary<string, List<ArrayPropertyData>> itemTypesProperty = new Dictionary<string, List<ArrayPropertyData>>();
            for (int cat = 0; cat < y.data.categories.Count; cat++)
            {
                if (y.data.categories[cat] is NormalCategory normalCat)
                {
                    for (int i = 0; i < normalCat.Data.Count; i++)
                    {
                        foreach (KeyValuePair<string, List<string>> entry in newItems)
                        {
                            if (normalCat.Data[i].Name.Equals(entry.Key) && normalCat.Data[i] is ArrayPropertyData)
                            {
                                if (!itemTypesProperty.ContainsKey(entry.Key)) itemTypesProperty.Add(entry.Key, new List<ArrayPropertyData>());
                                itemTypesProperty[entry.Key].Add((ArrayPropertyData)normalCat.Data[i]);
                            }
                        }
                    }
                }
            }

            foreach (KeyValuePair<string, List<string>> itemPaths in newItems)
            {
                if (!itemTypesProperty.ContainsKey(itemPaths.Key)) continue;
                foreach (string itemPath in itemPaths.Value)
                {
                    string realName = itemPath;
                    string className = Path.GetFileNameWithoutExtension(itemPath) + "_C";
                    if (itemPath.Contains("."))
                    {
                        string[] tData = itemPath.Split(new char[] { '.' });
                        realName = tData[0];
                        className = tData[1];
                    }

                    y.data.AddHeaderReference(realName);
                    y.data.AddHeaderReference(className);
                    y.data.AddHeaderReference("ObjectProperty");
                    Link newLink = new Link("/Script/Engine", "BlueprintGeneratedClass", y.data.AddLink("/Script/CoreUObject", "Package", 0, realName).Index, className, y.data);
                    int bigNewLink = y.data.AddLink(newLink);

                    for (int prop = 0; prop < itemTypesProperty.Count; prop++)
                    {
                        ArrayPropertyData currentItemTypesProperty = itemTypesProperty[itemPaths.Key][prop];
                        PropertyData[] usArrData = currentItemTypesProperty.Value;
                        int oldLen = usArrData.Length;
                        Array.Resize(ref usArrData, oldLen + 1);
                        usArrData[oldLen] = new ObjectPropertyData(currentItemTypesProperty.Name, y.data)
                        {
                            LinkValue = bigNewLink
                        };
                        itemTypesProperty[itemPaths.Key][prop].Value = usArrData;
                    }
                }
            }

            return y.WriteData(new BinaryReader(new MemoryStream(y.OriginalCopy)));
        }
    }
}
