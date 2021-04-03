using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UAssetAPI;
using UAssetAPI.PropertyTypes;

namespace AstroModIntegrator
{
    public class MissionBaker
    {
        private PakExtractor Extractor;
        public MissionBaker(PakExtractor extractor)
        {
            Extractor = extractor;
        }

        public MemoryStream Bake(string[] newTrailheads, byte[] superRawData)
        {
            BinaryReader yReader = new BinaryReader(new MemoryStream(superRawData));
            AssetWriter y = new AssetWriter
            {
                WillStoreOriginalCopyInMemory = true, WillWriteSectionSix = true, data = new AssetReader()
            };
            y.data.Read(yReader);
            y.OriginalCopy = superRawData;

            for (int cat = 0; cat < y.data.categories.Count; cat++)
            {
                if (y.data.categories[cat] is NormalCategory normalCat)
                {
                    if (y.data.GetHeaderReference(y.data.GetLinkReference(normalCat.ReferenceData.connection)) != "AstroSettings") continue;

                    for (int i = 0; i < normalCat.Data.Count; i++)
                    {
                        if (normalCat.Data[i].Name == "MissionData" && normalCat.Data[i] is ArrayPropertyData arrDat && arrDat.ArrayType == "ObjectProperty")
                        {
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
                        }
                    }
                }
            }

            return y.WriteData(new BinaryReader(new MemoryStream(y.OriginalCopy)));
        }
    }
}
