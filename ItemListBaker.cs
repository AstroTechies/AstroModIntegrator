﻿using System;
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
        public ItemListBaker()
        {

        }

        public UAsset Bake(Dictionary<string, List<string>> newItems, byte[] superRawData)
        {
            UAsset y = new UAsset(IntegratorUtils.EngineVersion);
            y.UseSeparateBulkDataFiles = true;
            y.Read(new AssetBinaryReader(new MemoryStream(superRawData), y));

            // Find some categories
            Dictionary<string, List<ArrayPropertyData>> itemTypesProperty = new Dictionary<string, List<ArrayPropertyData>>();
            for (int cat = 0; cat < y.Exports.Count; cat++)
            {
                if (y.Exports[cat] is NormalExport normalCat)
                {
                    for (int i = 0; i < normalCat.Data.Count; i++)
                    {
                        foreach (KeyValuePair<string, List<string>> entry in newItems)
                        {
                            string arrName = entry.Key;
                            if (entry.Key.Contains('.'))
                            {
                                string[] tData = entry.Key.Split(new char[] { '.' });
                                string catName = tData[0].ToLower();
                                arrName = tData[1];
                                if ((normalCat.ClassIndex.IsImport() ? normalCat.ClassIndex.ToImport(y).ObjectName.Value.Value : string.Empty).ToLower() != catName) continue;
                            }

                            if (normalCat.Data[i].Name.Value.Value.Equals(arrName) && normalCat.Data[i] is ArrayPropertyData)
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
                    string softClassName = Path.GetFileNameWithoutExtension(itemPath);
                    if (itemPath.Contains("."))
                    {
                        string[] tData = itemPath.Split(new char[] { '.' });
                        realName = tData[0];
                        className = tData[1];
                        softClassName = tData[1];
                    }

                    FPackageIndex bigNewLink = FPackageIndex.FromRawIndex(0);

                    for (int prop = 0; prop < itemTypesProperty[itemPaths.Key].Count; prop++)
                    {
                        ArrayPropertyData currentItemTypesProperty = itemTypesProperty[itemPaths.Key][prop];
                        PropertyData[] usArrData = currentItemTypesProperty.Value;
                        int oldLen = usArrData.Length;
                        Array.Resize(ref usArrData, oldLen + 1);
                        switch (currentItemTypesProperty.ArrayType.Value.Value)
                        {
                            case "ObjectProperty":
                                if (bigNewLink.Index == 0)
                                {
                                    y.AddNameReference(new FString(realName));
                                    y.AddNameReference(new FString(className));
                                    Import newLink = new Import("/Script/Engine", "BlueprintGeneratedClass", y.AddImport(new Import("/Script/CoreUObject", "Package", FPackageIndex.FromRawIndex(0), realName)), className);
                                    bigNewLink = y.AddImport(newLink);
                                }

                                usArrData[oldLen] = new ObjectPropertyData(currentItemTypesProperty.Name)
                                {
                                    Value = bigNewLink
                                };
                                itemTypesProperty[itemPaths.Key][prop].Value = usArrData;
                                break;
                            case "SoftObjectProperty":
                                y.AddNameReference(new FString(realName));
                                y.AddNameReference(new FString(realName + "." + softClassName));
                                usArrData[oldLen] = new SoftObjectPropertyData(currentItemTypesProperty.Name)
                                {
                                    Value = new FName(realName + "." + softClassName),
                                };
                                itemTypesProperty[itemPaths.Key][prop].Value = usArrData;
                                break;
                        }
                        currentItemTypesProperty.Value = usArrData;
                    }
                }
            }

            return y;
        }
    }
}
