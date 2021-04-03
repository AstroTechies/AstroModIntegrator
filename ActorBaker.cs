using System;
using System.Collections.Generic;
using System.IO;
using UAssetAPI;
using UAssetAPI.PropertyTypes;
using UAssetAPI.StructTypes;

namespace AstroModIntegrator
{
    public class ActorBaker
    {
        private readonly CategoryReference refData1B; // ObjectProperty
        private readonly CategoryReference refData2B; // Template category
        private readonly CategoryReference refData3B; // SCS_Node

        public ActorBaker()
        {
            AssetReader y = new AssetReader(new BinaryReader(new MemoryStream(Properties.Resources.ActorTemplate)));
            refData1B = y.categories[6].ReferenceData;
            refData2B = y.categories[5].ReferenceData;
            refData3B = y.categories[10].ReferenceData;
        }

        public MemoryStream Bake(string[] newComponents, byte[] superRawData)
        {
            BinaryReader yReader = new BinaryReader(new MemoryStream(superRawData));
            AssetWriter y = new AssetWriter
            {
                WillStoreOriginalCopyInMemory = true, WillWriteSectionSix = true, data = new AssetReader()
            };
            y.data.Read(yReader);
            y.OriginalCopy = superRawData;

            int scsLocation = -1;
            int bgcLocation = -1;
            for (int i = 0; i < y.data.categories.Count; i++)
            {
                Category baseUs = y.data.categories[i];
                if (baseUs is NormalCategory us)
                {
                    switch (y.data.GetHeaderReference(y.data.GetLinkReference(us.ReferenceData.connection)))
                    {
                        case "SimpleConstructionScript":
                            scsLocation = i;
                            break;
                        case "BlueprintGeneratedClass":
                            bgcLocation = i;
                            break;
                    }
                }
            }
            if (scsLocation < 0) throw new FormatException("Unable to find SimpleConstructionScript");
            if (bgcLocation < 0) throw new FormatException("Unable to find BlueprintGeneratedClass");

            int objectPropertyLink = y.data.SearchForLink((ulong)y.data.SearchHeaderReference("/Script/CoreUObject"), (ulong)y.data.SearchHeaderReference("Class"), (ulong)y.data.SearchHeaderReference("ObjectProperty"));
            int scsNodeLink = y.data.SearchForLink((ulong)y.data.SearchHeaderReference("/Script/CoreUObject"), (ulong)y.data.SearchHeaderReference("Class"), (ulong)y.data.SearchHeaderReference("SCS_Node"));
            byte[] noneRef = BitConverter.GetBytes((long)y.data.SearchHeaderReference("None"));

            y.data.AddHeaderReference("bAutoActivate");
            foreach (string componentPathRaw in newComponents)
            {
                CategoryReference refData1 = new CategoryReference(refData1B);
                CategoryReference refData2 = new CategoryReference(refData2B);
                CategoryReference refData3 = new CategoryReference(refData3B);

                string componentPath = componentPathRaw;
                string component = Path.GetFileNameWithoutExtension(componentPathRaw);
                if (componentPathRaw.Contains("."))
                {
                    string[] tData = componentPathRaw.Split(new char[] { '.' });
                    componentPath = tData[0];
                    component = tData[1].Remove(tData[1].Length - 2);
                }
                y.data.AddHeaderReference(componentPath);
                y.data.AddHeaderReference(component + "_C");

                Link newLink = new Link("/Script/Engine", "BlueprintGeneratedClass", y.data.AddLink("/Script/CoreUObject", "Package", 0, componentPath).Index, component + "_C", y.data);
                int bigNewLink = y.data.AddLink(newLink);
                refData2.connection = bigNewLink;
                refData2.typeIndex = y.data.AddHeaderReference(component + "_GEN_VARIABLE");

                refData1.connection = objectPropertyLink;
                refData1.typeIndex = y.data.AddHeaderReference(component);

                refData3.connection = scsNodeLink;
                refData3.typeIndex = y.data.AddHeaderReference("SCS_Node");

                List<byte> rawData = new List<byte>();

                // Here we specify the raw data for our ObjectProperty category, including necessary flags and such
                rawData.AddRange(noneRef);
                rawData.AddRange(new byte[] {
                    0x00,
                    0x00,
                    0x00,
                    0x00,
                    0x01,
                    0x00,
                    0x00,
                    0x00,

                    0x04,
                    0x00,
                    0x00,
                    0x00,
                    0x04,
                    0x00,
                    0x00,
                    0x00,
                });
                rawData.AddRange(noneRef);
                rawData.Add((byte)0);
                rawData.AddRange(BitConverter.GetBytes((int)bigNewLink));

                refData1.link = bgcLocation + 1; // BlueprintGeneratedClass category
                refData2.link = bgcLocation + 1; // BlueprintGeneratedClass category
                refData3.link = scsLocation + 1;

                // Note that category links are set to one more than you'd think since categories in the category list index from 1 instead of 0

                // First we add the template category
                y.data.categories.Add(new NormalCategory(new List<PropertyData>
                {
                    new BoolPropertyData("bAutoActivate", y.data)
                    {
                        Value = true
                    }
                }, refData2, y.data, new byte[4] { 0, 0, 0, 0 }));

                // Then the ObjectProperty category
                y.data.categories.Add(new RawCategory(rawData.ToArray(), refData1, y.data, new byte[0]));

                // Then the SCS_Node
                y.data.categories.Add(new NormalCategory(new List<PropertyData>
                {
                    new ObjectPropertyData("ComponentClass", y.data)
                    {
                        LinkValue = bigNewLink
                    },
                    new ObjectPropertyData("ComponentTemplate", y.data)
                    {
                        LinkValue = y.data.categories.Count - 1 // the first NormalCategory
                    },
                    new StructPropertyData("VariableGuid", y.data, "Guid")
                    {
                        Value = new List<PropertyData>
                        {
                            new GuidPropertyData("VariableGuid", y.data)
                            {
                                Value = Guid.NewGuid()
                            }
                        }
                    },
                    new NamePropertyData("InternalVariableName", y.data)
                    {
                        Value = component,
                        Value2 = 0
                    }
                }, refData3, y.data, new byte[4] { 0, 0, 0, 0 }));

                // We update the BlueprintGeneratedClass data to include our new ActorComponent
                ((BlueprintGeneratedClassCategory)y.data.categories[bgcLocation]).IndexData.Add(y.data.categories.Count - 1); // the RawCategory

                // Here we update the SimpleConstructionScript so that the parser constructs our new ActorComponent
                NormalCategory scsCategory = (NormalCategory)y.data.categories[scsLocation];
                for (int j = 0; j < scsCategory.Data.Count; j++)
                {
                    PropertyData bit = scsCategory.Data[j];
                    if (bit.Type.Equals("ArrayProperty"))
                    {
                        switch (bit.Name)
                        {
                            case "AllNodes":
                            case "RootNodes":
                                PropertyData[] ourArr = ((ArrayPropertyData)bit).Value;
                                int oldSize = ourArr.Length;
                                Array.Resize(ref ourArr, oldSize + 1);
                                refData3.garbage1 = oldSize + 2;
                                ourArr[oldSize] = new ObjectPropertyData(bit.Name, y.data)
                                {
                                    LinkValue = y.data.categories.Count // the SCS_Node
                                };
                                ((ArrayPropertyData)bit).Value = ourArr;
                                break;
                        }
                    }
                }
            }

            return y.WriteData(new BinaryReader(new MemoryStream(y.OriginalCopy)));
        }
    }
}
