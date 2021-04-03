using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace AstroModIntegrator
{
    public class ModIntegrator
    {
        // Settings //
        public bool RefuseMismatchedConnections;
        public List<string> OptionalModIDs;
        // End Settings //

        private static string[] MapPaths = new string[] {
            "Astro/Content/Maps/Staging_T2.umap",
            "Astro/Content/Maps/TutorialMoon_Prototype_v2.umap",
            "Astro/Content/Maps/test/BasicSphereT2.umap"
        };

        private byte[] FindFile(string target, Dictionary<string, byte[]> createdPakData, PakExtractor ourExtractor)
        {
            if (createdPakData.ContainsKey(target)) return createdPakData[target];
            if (ourExtractor.HasPath(target)) return ourExtractor.ReadRaw(target);
            return null;
        }

        public void IntegrateMods(string paksPath, string installPath) // @"C:\Users\<CLIENT USERNAME>\AppData\Local\Astro\Saved\Paks", @"C:\Program Files (x86)\Steam\steamapps\common\ASTRONEER\Astro\Content\Paks"
        {
            Directory.CreateDirectory(paksPath);
            string[] files = Directory.GetFiles(paksPath, "*_P.pak", SearchOption.TopDirectoryOnly);

            int modCount = 0;
            Dictionary<string, List<string>> newComponents = new Dictionary<string, List<string>>();
            Dictionary<string, Dictionary<string, List<string>>> newItems = new Dictionary<string, Dictionary<string, List<string>>>();
            List<string> newPersistentActors = new List<string>();
            List<string> newTrailheads = new List<string>();
            List<Metadata> allMods = new List<Metadata>();
            foreach (string file in files)
            {
                using (FileStream f = new FileStream(file, FileMode.Open, FileAccess.Read))
                {
                    Metadata us = null;
                    try
                    {
                        us = new PakExtractor(new BinaryReader(f)).ReadMetadata();
                    }
                    catch
                    {
                        continue;
                    }

                    if (us == null || us.ModID == "AstroModIntegrator") continue;
                    modCount++;
                    allMods.Add(us);

                    Dictionary<string, List<string>> theseComponents = us.LinkedActorComponents;
                    if (theseComponents != null)
                    {
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

                    Dictionary<string, Dictionary<string, List<string>>> theseItems = us.ItemListEntries;
                    if (theseItems != null)
                    {
                        foreach (KeyValuePair<string, Dictionary<string, List<string>>> entry in theseItems)
                        {
                            if (newItems.ContainsKey(entry.Key))
                            {
                                foreach (KeyValuePair<string, List<string>> entry2 in entry.Value)
                                {
                                    if (newItems[entry.Key].ContainsKey(entry2.Key))
                                    {
                                        newItems[entry.Key][entry2.Key].AddRange(entry2.Value);
                                    }
                                    else
                                    {
                                        newItems[entry.Key].Add(entry2.Key, entry2.Value);
                                    }
                                }
                            }
                            else
                            {
                                newItems.Add(entry.Key, entry.Value);
                            }
                        }
                    }

                    List<string> thesePersistentActors = us.PersistentActors;
                    if (thesePersistentActors != null)
                    {
                        newPersistentActors.AddRange(thesePersistentActors);
                    }

                    List<string> theseTrailheads = us.MissionTrailheads;
                    if (theseTrailheads != null)
                    {
                        newTrailheads.AddRange(theseTrailheads);
                    }
                }
            }

            Dictionary<string, byte[]> createdPakData = new Dictionary<string, byte[]>
            {
                { "metadata.json", StarterPakData["metadata.json"] }
            };

            if (modCount > 0)
            {
                // Apply static files
                createdPakData = StarterPakData.ToDictionary(entry => entry.Key, entry => (byte[])entry.Value.Clone());

                if (!newComponents.ContainsKey("/Game/Globals/PlayControllerInstance")) newComponents.Add("/Game/Globals/PlayControllerInstance", new List<string>());
                newComponents["/Game/Globals/PlayControllerInstance"].Add("/Game/Integrator/ServerModComponent");

                // Generate mods data table
                var dtb = new DataTableBaker(this);
                createdPakData["Astro/Content/Integrator/ListOfMods.uasset"] = dtb.Bake(allMods.ToArray(), OptionalModIDs, createdPakData["Astro/Content/Integrator/ListOfMods.uasset"]).ToArray();
                createdPakData["Astro/Content/Integrator/IntegratorStatics.uasset"] = dtb.Bake2(createdPakData["Astro/Content/Integrator/IntegratorStatics.uasset"]).ToArray();
            }

            string[] realPakPaths = Directory.GetFiles(installPath, "*.pak", SearchOption.TopDirectoryOnly);
            foreach (string realPakPath in realPakPaths)
            {
                using (FileStream f = new FileStream(realPakPath, FileMode.Open, FileAccess.Read))
                {
                    PakExtractor ourExtractor;
                    try
                    {
                        ourExtractor = new PakExtractor(new BinaryReader(f));
                    }
                    catch
                    {
                        continue;
                    }

                    var actorBaker = new ActorBaker();
                    var itemListBaker = new ItemListBaker(ourExtractor);
                    var missionBaker = new MissionBaker(ourExtractor);
                    var levelBaker = new LevelBaker(ourExtractor, paksPath);

                    // Patch level for persisent actors
                    if (newPersistentActors.Count > 0)
                    {
                        foreach (string mapPath in MapPaths)
                        {
                            byte[] mapPathData = FindFile(mapPath, createdPakData, ourExtractor);
                            if (mapPathData != null) createdPakData[mapPath] = levelBaker.Bake(newPersistentActors.ToArray(), mapPathData).ToArray();
                        }
                    }

                    // Patch level for missions
                    if (newTrailheads.Count > 0)
                    {
                        foreach (string mapPath in MapPaths)
                        {
                            byte[] mapPathData = FindFile(mapPath, createdPakData, ourExtractor);
                            if (mapPathData != null) createdPakData[mapPath] = missionBaker.Bake(newTrailheads.ToArray(), mapPathData).ToArray();
                        }
                    }

                    // Add components
                    foreach (KeyValuePair<string, List<string>> entry in newComponents)
                    {
                        string establishedPath = entry.Key.ConvertGamePathToAbsolutePath();

                        byte[] actorData = FindFile(establishedPath, createdPakData, ourExtractor);
                        if (actorData == null) continue;
                        try
                        {
                            createdPakData[establishedPath] = actorBaker.Bake(entry.Value.ToArray(), actorData).ToArray();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(ex.ToString());
                        }
                    }

                    // Add new item entries
                    foreach (KeyValuePair<string, Dictionary<string, List<string>>> entry in newItems)
                    {
                        string establishedPath = entry.Key.ConvertGamePathToAbsolutePath();

                        byte[] actorData = FindFile(establishedPath, createdPakData, ourExtractor);
                        if (actorData == null) continue;
                        try
                        {
                            createdPakData[establishedPath] = itemListBaker.Bake(entry.Value, actorData).ToArray();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(ex.ToString());
                        }
                    }
                }
            }

            byte[] pakData = PakBaker.Bake(createdPakData);

            using (FileStream f = new FileStream(Path.Combine(paksPath, @"999-AstroModIntegrator_P.pak"), FileMode.Create, FileAccess.Write))
            {
                f.Write(pakData, 0, pakData.Length);
            }
        }

        private Dictionary<string, byte[]> StarterPakData = new Dictionary<string, byte[]>();
        public ModIntegrator()
        {
            OptionalModIDs = new List<string>();

            // Include static assets
            PakExtractor staticAssetsExtractor = new PakExtractor(new BinaryReader(new MemoryStream(Properties.Resources.IntegratorStaticAssets)));
            foreach (KeyValuePair<string, long> entry in staticAssetsExtractor.PathToOffset)
            {
                StarterPakData[entry.Key] = staticAssetsExtractor.ReadRaw(entry.Value);
            }
        }
    }
}
