using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UAssetAPI;
using UAssetAPI.PropertyTypes;
using UAssetAPI.StructTypes;

namespace AstroModIntegrator
{
    public class SCS_Node
    {
        public string InternalVariableName; // string name
        public int TypeLink;
        public int AttachParent = -1; // parent category
        public int OriginalCategory; // original category in the source file
    }

    public class LevelBaker
    {
        private PakExtractor Extractor;
        private string InstallPath;
        private readonly CategoryReference refData1B; // Actor template
        private readonly CategoryReference refData2B; // SceneComponent

        public LevelBaker(PakExtractor extractor, string installPath)
        {
            Extractor = extractor;
            InstallPath = installPath;

            AssetReader y = new AssetReader(new BinaryReader(new MemoryStream(Properties.Resources.LevelTemplate)));
            refData1B = y.categories[2].ReferenceData;
            refData2B = y.categories[11].ReferenceData;

            InitializeSearch();
        }

        internal Dictionary<string, string> SearchLookup; // file to path --> pak you can find it in
        internal void InitializeSearch()
        {
            SearchLookup = new Dictionary<string, string>();
            string[] realPakPaths = Directory.GetFiles(InstallPath, "*.pak", SearchOption.TopDirectoryOnly);
            foreach (string realPakPath in realPakPaths)
            {
                using (FileStream f = new FileStream(realPakPath, FileMode.Open, FileAccess.Read))
                {
                    try
                    {
                        PakExtractor ourExtractor = new PakExtractor(new BinaryReader(f));
                        foreach (KeyValuePair<string, long> entry in ourExtractor.PathToOffset)
                        {
                            SearchLookup[entry.Key] = realPakPath;
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }
            }
        }

        internal byte[] SearchInAllPaksForPath(string searchingPath)
        {
            if (Extractor.HasPath(searchingPath)) return Extractor.ReadRaw(searchingPath);
            if (SearchLookup.ContainsKey(searchingPath))
            {
                try
                {
                    using (FileStream f = new FileStream(SearchLookup[searchingPath], FileMode.Open, FileAccess.Read))
                    {
                        try
                        {
                            PakExtractor ourExtractor = new PakExtractor(new BinaryReader(f));
                            if (ourExtractor.HasPath(searchingPath)) return ourExtractor.ReadRaw(searchingPath);
                        }
                        catch { }
                    }
                }
                catch (IOException)
                {
                    return null;
                }
            }
            
            return null;
        }

        /*
            Game plan:
            1. Find the Level category
            2. Find the link with the Property SceneComponent
            3. Dig into the vanilla pak and the mod pak, try to find the connecting actor, add its nodes in the SimpleConstructionScript under BlueprintCreatedComponents (garbage1 = 0 no problem)
            4. Create the SceneComponent (garbage1 = 0), no RelativeLocation or UCSModifiedProperties, CreationMethod = EComponentCreationMethod::SimpleConstructionScript, bNetAddressable = 1
            5. Create the new Actor_C category, set its Linkage to the Level category, set the garbage1 to 0 (maybe random number idk), DefaultSceneRoot & RootComponent = the matching SceneComponent
        */

        public MemoryStream Bake(string[] newComponents, string[] newTrailheads, byte[] superRawData)
        {
            BinaryReader yReader = new BinaryReader(new MemoryStream(superRawData));
            AssetWriter y = new AssetWriter
            {
                WillStoreOriginalCopyInMemory = true, WillWriteSectionSix = true, data = new AssetReader()
            };
            y.data.Read(yReader);
            y.OriginalCopy = superRawData;

            // Missions
            if (newTrailheads.Length > 0)
            {
                for (int cat = 0; cat < y.data.categories.Count; cat++)
                {
                    if (y.data.categories[cat] is NormalCategory normalCat)
                    {
                        if (y.data.GetHeaderReference(y.data.GetLinkReference(normalCat.ReferenceData.connection)) != "AstroSettings") continue;

                        for (int i = 0; i < normalCat.Data.Count; i++)
                        {
                            if (normalCat.Data[i].Name == "MissionData" && normalCat.Data[i] is ArrayPropertyData arrDat && arrDat.ArrayType == "ObjectProperty")
                            {
                                y.data.AddHeaderReference("AstroMissionDataAsset");

                                PropertyData[] usArrData = arrDat.Value;
                                int oldLen = usArrData.Length;
                                Array.Resize(ref usArrData, usArrData.Length + newTrailheads.Length);
                                for (int j = 0; j < newTrailheads.Length; j++)
                                {
                                    string realName = newTrailheads[j];
                                    string softClassName = Path.GetFileNameWithoutExtension(realName);

                                    y.data.AddHeaderReference(realName);
                                    y.data.AddHeaderReference(softClassName);
                                    Link newLink = new Link("/Script/Astro", "AstroMissionDataAsset", y.data.AddLink("/Script/CoreUObject", "Package", 0, realName).Index, softClassName, y.data);
                                    int bigNewLink = y.data.AddLink(newLink);

                                    usArrData[oldLen + j] = new ObjectPropertyData(arrDat.Name, y.data)
                                    {
                                        LinkValue = bigNewLink
                                    };
                                }
                                arrDat.Value = usArrData;
                                break;
                            }
                        }
                        break;
                    }
                }
            }

            if (newComponents.Length == 0) return y.WriteData(new BinaryReader(new MemoryStream(y.OriginalCopy)));

            LevelCategory levelCategory = null;
            int levelLocation = -1;
            for (int i = 0; i < y.data.categories.Count; i++)
            {
                Category baseUs = y.data.categories[i];
                if (baseUs is LevelCategory levelUs)
                {
                    levelCategory = levelUs;
                    levelLocation = i;
                    break;
                }
            }
            if (levelLocation < 0) throw new FormatException("Unable to find Level category");

            // Preliminary header reference additions
            y.data.AddHeaderReference("bHidden");
            y.data.AddHeaderReference("bNetAddressable");
            y.data.AddHeaderReference("CreationMethod");
            y.data.AddHeaderReference("EComponentCreationMethod");
            y.data.AddHeaderReference("EComponentCreationMethod::SimpleConstructionScript");
            y.data.AddHeaderReference("BlueprintCreatedComponents");
            y.data.AddHeaderReference("AttachParent");
            y.data.AddHeaderReference("RootComponent");

            foreach (string componentPathRaw in newComponents)
            {
                CategoryReference refData1 = new CategoryReference(refData1B);
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
                refData1.connection = bigNewLink;
                refData1.typeIndex = y.data.AddHeaderReference(component);

                // Note that category links are set to one more than you'd think since categories in the category list index from 1 instead of 0

                refData1.garbage1 = 0;
                refData1.link = levelLocation + 1; // Level category

                // First we see if we can find the actual asset it's referring to
                List<SCS_Node> allBlueprintCreatedComponents = new List<SCS_Node>();
                byte[] foundData = SearchInAllPaksForPath(componentPath.ConvertGamePathToAbsolutePath());
                if (foundData != null && foundData.Length > 0)
                {
                    // If we can find the asset, then we read the asset and hop straight to the SimpleConstructionScript
                    AssetReader foundDataReader = new AssetReader();
                    foundDataReader.Read(new BinaryReader(new MemoryStream(foundData)), null, null);

                    int scsLocation = -1;
                    for (int i = 0; i < foundDataReader.categories.Count; i++)
                    {
                        Category foundCategory = foundDataReader.categories[i];
                        if (foundCategory is NormalCategory normalFoundCategory)
                        {
                            string nm = foundDataReader.GetHeaderReference(foundDataReader.GetLinkReference(normalFoundCategory.ReferenceData.connection));
                            switch (nm)
                            {
                                case "SimpleConstructionScript":
                                    scsLocation = i;
                                    break;
                            }
                        }
                    }

                    if (scsLocation >= 0)
                    {
                        List<int> knownNodeCategories = new List<int>();
                        NormalCategory scsCategory = (NormalCategory)foundDataReader.categories[scsLocation];
                        for (int j = 0; j < scsCategory.Data.Count; j++)
                        {
                            PropertyData bit = scsCategory.Data[j];
                            if (bit is ArrayPropertyData arrBit && arrBit.ArrayType == "ObjectProperty" && bit.Name == "AllNodes")
                            {
                                foreach (ObjectPropertyData objProp in arrBit.Value)
                                {
                                    if (objProp.LinkValue > 0) knownNodeCategories.Add(objProp.LinkValue);
                                }
                            }
                        }

                        Dictionary<int, int> knownParents = new Dictionary<int, int>();
                        foreach (int knownNodeCategory in knownNodeCategories)
                        {
                            Category knownCat = foundDataReader.categories[knownNodeCategory - 1];
                            string nm = foundDataReader.GetHeaderReference(foundDataReader.GetLinkReference(knownCat.ReferenceData.connection));
                            if (nm != "SCS_Node") continue;
                            if (knownCat is NormalCategory knownNormalCat)
                            {
                                SCS_Node newSCS = new SCS_Node();
                                newSCS.InternalVariableName = "Unknown";
                                newSCS.OriginalCategory = knownNodeCategory;
                                Link knownTypeLink1 = null;
                                Link knownTypeLink2 = null;

                                foreach (PropertyData knownNormalCatProp in knownNormalCat.Data)
                                {
                                    switch (knownNormalCatProp.Name)
                                    {
                                        case "InternalVariableName":
                                            if (knownNormalCatProp is NamePropertyData) newSCS.InternalVariableName = ((NamePropertyData)knownNormalCatProp).Value;
                                            break;
                                        case "ComponentClass":
                                            if (knownNormalCatProp is ObjectPropertyData) knownTypeLink1 = ((ObjectPropertyData)knownNormalCatProp).Value;
                                            knownTypeLink2 = foundDataReader.GetLinkAt(knownTypeLink1.Linkage);
                                            break;
                                        case "ChildNodes":
                                            if (knownNormalCatProp is ArrayPropertyData arrData2 && arrData2.ArrayType == "ObjectProperty")
                                            {
                                                foreach (ObjectPropertyData knownNormalCatPropChildren in arrData2.Value)
                                                {
                                                    knownParents.Add(knownNormalCatPropChildren.LinkValue, knownNodeCategory);
                                                }
                                            }
                                            break;
                                    }
                                }

                                if (knownTypeLink1 != null && knownTypeLink2 != null)
                                {
                                    Link prospectiveLink2 = new Link();
                                    prospectiveLink2.Base = (ulong)y.data.AddHeaderReference(foundDataReader.GetHeaderReference((int)knownTypeLink2.Base));
                                    prospectiveLink2.Class = (ulong)y.data.AddHeaderReference(foundDataReader.GetHeaderReference((int)knownTypeLink2.Class));
                                    prospectiveLink2.Linkage = knownTypeLink2.Linkage;
                                    prospectiveLink2.Property = (ulong)y.data.AddHeaderReference(foundDataReader.GetHeaderReference((int)knownTypeLink2.Property));

                                    int addedLink = y.data.SearchForLink(prospectiveLink2.Base, prospectiveLink2.Class, prospectiveLink2.Linkage, prospectiveLink2.Property);
                                    if (addedLink >= 0) addedLink = y.data.AddLink(prospectiveLink2);

                                    Link prospectiveLink1 = new Link();
                                    prospectiveLink1.Base = (ulong)y.data.AddHeaderReference(foundDataReader.GetHeaderReference((int)knownTypeLink1.Base));
                                    prospectiveLink1.Class = (ulong)y.data.AddHeaderReference(foundDataReader.GetHeaderReference((int)knownTypeLink1.Class));
                                    prospectiveLink1.Property = (ulong)y.data.AddHeaderReference(foundDataReader.GetHeaderReference((int)knownTypeLink1.Property));
                                    prospectiveLink1.Linkage = addedLink;

                                    int newTypeLink = y.data.SearchForLink(prospectiveLink1.Base, prospectiveLink1.Class, prospectiveLink1.Linkage, prospectiveLink1.Property);
                                    if (newTypeLink >= 0) newTypeLink = y.data.AddLink(prospectiveLink1);
                                    newSCS.TypeLink = newTypeLink;
                                }

                                allBlueprintCreatedComponents.Add(newSCS);
                            }
                        }

                        foreach (SCS_Node node in allBlueprintCreatedComponents)
                        {
                            if (knownParents.ContainsKey(node.OriginalCategory)) node.AttachParent = knownParents[node.OriginalCategory];
                        }
                    }
                }

                // Then we add all our child components
                int templateCategoryPointer = y.data.categories.Count + allBlueprintCreatedComponents.Count + 1;

                List<ObjectPropertyData> BlueprintCreatedComponentsSerializedList = new List<ObjectPropertyData>();
                List<ObjectPropertyData> AttachParentDueForCorrection = new List<ObjectPropertyData>();
                Dictionary<string, int> NodeNameToCatIndex = new Dictionary<string, int>();
                Dictionary<int, int> OldCatToNewCat = new Dictionary<int, int>();
                foreach (SCS_Node blueprintCreatedComponent in allBlueprintCreatedComponents)
                {
                    CategoryReference refData2 = new CategoryReference(refData2B);

                    refData2.connection = blueprintCreatedComponent.TypeLink;
                    refData2.typeIndex = y.data.AddHeaderReference(blueprintCreatedComponent.InternalVariableName);
                    refData2.garbage1 = 0; // unknown if this needs to be randomized or something
                    refData2.link = templateCategoryPointer; // Template category

                    var determinedPropData = new List<PropertyData>
                    {
                        new BoolPropertyData("bNetAddressable", y.data)
                        {
                            Value = true,
                        },
                        new EnumPropertyData("CreationMethod", y.data)
                        {
                            EnumType = "EComponentCreationMethod",
                            Value = "EComponentCreationMethod::SimpleConstructionScript"
                        }
                    };

                    if (blueprintCreatedComponent.AttachParent >= 0)
                    {
                        var nextOPD = new ObjectPropertyData("AttachParent", y.data)
                        {
                            LinkValue = blueprintCreatedComponent.AttachParent
                        };
                        AttachParentDueForCorrection.Add(nextOPD);
                        determinedPropData.Add(nextOPD);
                    }

                    y.data.categories.Add(new NormalCategory(determinedPropData, refData2, y.data, new byte[4] { 0, 0, 0, 0 }));
                    BlueprintCreatedComponentsSerializedList.Add(new ObjectPropertyData("BlueprintCreatedComponents", y.data)
                    {
                        LinkValue = y.data.categories.Count
                    });
                    NodeNameToCatIndex.Add(blueprintCreatedComponent.InternalVariableName, y.data.categories.Count);
                    OldCatToNewCat.Add(blueprintCreatedComponent.OriginalCategory, y.data.categories.Count);

                    y.data.AddLink(new Link((ulong)y.data.AddHeaderReference("/Script/Engine"), y.data.GetLinkAt(blueprintCreatedComponent.TypeLink).Property, refData1.connection, (ulong)y.data.AddHeaderReference(blueprintCreatedComponent.InternalVariableName + "_GEN_VARIABLE")));
                }

                foreach (ObjectPropertyData attachParentCorrecting in AttachParentDueForCorrection)
                {
                    attachParentCorrecting.LinkValue = OldCatToNewCat[attachParentCorrecting.LinkValue];
                }

                // Then we add the template category
                var templateDeterminedPropData = new List<PropertyData>
                {
                    new BoolPropertyData("bHidden", y.data)
                    {
                        Value = true
                    },
                    new ArrayPropertyData("BlueprintCreatedComponents", y.data)
                    {
                        ArrayType = "ObjectProperty",
                        Value = BlueprintCreatedComponentsSerializedList.ToArray()
                    }
                };

                foreach (KeyValuePair<string, int> entry in NodeNameToCatIndex)
                {
                    if (entry.Key == "DefaultSceneRoot")
                    {
                        templateDeterminedPropData.Add(new ObjectPropertyData("RootComponent", y.data)
                        {
                            LinkValue = entry.Value
                        });
                    }
                    templateDeterminedPropData.Add(new ObjectPropertyData(entry.Key, y.data)
                    {
                        LinkValue = entry.Value
                    });
                }

                y.data.categories.Add(new NormalCategory(templateDeterminedPropData, refData1, y.data, new byte[4] { 0, 0, 0, 0 }));

                // Add the template category to the level category
                levelCategory.IndexData.Add(y.data.categories.Count);
            }

            return y.WriteData(new BinaryReader(new MemoryStream(y.OriginalCopy)));
        }
    }
}
