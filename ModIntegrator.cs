﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AstroModIntegrator
{
    public static class ModIntegrator
    {
        public static void IntegrateMods(string paksPath, string installPath) // @"C:\Users\<CLIENT USERNAME>\AppData\Local\Astro\Saved\Paks", @"C:\Program Files (x86)\Steam\steamapps\common\ASTRONEER\Astro\Content\Paks"
        {
            Directory.CreateDirectory(paksPath);
            string[] files = Directory.GetFiles(paksPath, "*_P.pak", SearchOption.TopDirectoryOnly);

            Dictionary<string, List<string>> newComponents = new Dictionary<string, List<string>>();
            foreach (string file in files)
            {
                using (FileStream f = new FileStream(file, FileMode.Open, FileAccess.Read))
                {
                    Metadata us = new MetadataExtractor(new BinaryReader(f)).Read();
                    if (us == null) continue;
                    Dictionary<string, List<string>> theseComponents = us.LinkedActorComponents;
                    if (theseComponents == null) continue;

                    foreach (KeyValuePair<string, List<string>> entry in theseComponents)
                    {
                        if (newComponents.ContainsKey(entry.Key))
                        {
                            newComponents[entry.Key].AddRange(entry.Value);
                        }
                        else
                        {
                            newComponents.Add(entry.Key, entry.Value);
                        }
                    }
                }
            }

            string decidedNewMetadata = "{\"name\":\"AstroModLoader Mod Integrator\",\"author\":\"\",\"description\":\"Pak file auto-generated by AstroModLoader.\",\"version\":\"0.1.0\",\"astro_build\":\"0.0.0.0\",\"sync\":\"serverclient\"}";

            var actorBaker = new ActorBaker();

            Dictionary<string, byte[]> createdPakData = new Dictionary<string, byte[]>
            {
                { "metadata.json", Encoding.UTF8.GetBytes(decidedNewMetadata) },
                //{ "Astro/Content/Globals/PlayControllerInstance.uasset", actorBaker.Bake(newComponents.ToArray()).ToArray()}
            };

            string realPakPath = Path.Combine(installPath, "Astro-WindowsNoEditor.pak");
            using (FileStream f = new FileStream(realPakPath, FileMode.Open, FileAccess.Read))
            {
                MetadataExtractor ourExtractor = new MetadataExtractor(new BinaryReader(f));
                foreach (KeyValuePair<string, List<string>> entry in newComponents)
                {
                    string establishedPath = entry.Key;
                    if (!establishedPath.Substring(0, 5).Equals("/Game")) continue;
                    establishedPath = Path.ChangeExtension("Astro/Content" + establishedPath.Substring(5), ".uasset");
                    byte[] actorData = ourExtractor.ReadRaw(establishedPath);
                    createdPakData.Add(establishedPath, actorBaker.Bake(entry.Value.ToArray(), actorData).ToArray());
                }
            }

            byte[] pakData = PakBaker.Bake(createdPakData);

            using (FileStream f = new FileStream(Path.Combine(paksPath, @"999-AstroModLoader_P.pak"), FileMode.Create, FileAccess.Write))
            {
                f.Write(pakData, 0, pakData.Length);
            }
        }
    }
}
