using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace AstroModIntegrator
{
    public class ModIntegrator
    {
        // Settings //
        public bool RefuseMismatchedConnections;
        public List<string> OptionalModIDs;
        // End Settings //

        // Exposed Fields //
        public Version DetectedAstroBuild;
        // End Exposed Fields //

        private static string[] MapPaths = new string[] {
            "Astro/Content/Maps/Staging_T2.umap",
            "Astro/Content/Maps/Staging_T2_PackedPlanets_Switch.umap",
            //"Astro/Content/Maps/TutorialMoon_Prototype_v2.umap", // Tutorial not integrated for performance
            "Astro/Content/Maps/test/BasicSphereT2.umap",
        };

        internal byte[] FindFile(string target, PakExtractor ourExtractor)
        {
            if (CreatedPakData.ContainsKey(target)) return CreatedPakData[target];
            return SearchInAllPaksForPath(target, ourExtractor, false);
        }

        internal Dictionary<string, string> SearchLookup; // file to path --> pak you can find it in
        internal void InitializeSearch(string installPath)
        {
            SearchLookup = new Dictionary<string, string>();
            string[] realPakPaths = Directory.GetFiles(installPath, "*_P.pak", SearchOption.TopDirectoryOnly);
            foreach (string realPakPath in realPakPaths)
            {
                using (FileStream f = new FileStream(realPakPath, FileMode.Open, FileAccess.Read))
                {
                    try
                    {
                        PakExtractor ourExtractor = new PakExtractor(new BinaryReader(f));

                        Metadata us = null;
                        try
                        {
                            us = ourExtractor.ReadMetadata();
                        }
                        catch { }

                        if (us == null || IntegratorUtils.IgnoredModIDs.Contains(us.ModID)) continue;

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

        internal byte[] SearchInAllPaksForPath(string searchingPath, PakExtractor fullExtractor, bool checkMainPakFirst = true)
        {
            if (checkMainPakFirst && fullExtractor.HasPath(searchingPath)) return fullExtractor.ReadRaw(searchingPath);
            if (SearchLookup.ContainsKey(searchingPath))
            {
                try
                {
                    using (FileStream f = new FileStream(SearchLookup[searchingPath], FileMode.Open, FileAccess.Read))
                    {
                        try
                        {
                            PakExtractor modPakExtractor = new PakExtractor(new BinaryReader(f));
                            if (modPakExtractor.HasPath(searchingPath)) return modPakExtractor.ReadRaw(searchingPath);
                        }
                        catch { }
                    }
                }
                catch (IOException)
                {
                    
                }
            }

            if (!checkMainPakFirst && fullExtractor.HasPath(searchingPath)) return fullExtractor.ReadRaw(searchingPath);
            return null;
        }

        private Dictionary<string, byte[]> CreatedPakData;

        public void IntegrateMods(string paksPath, string installPath) // @"C:\Users\<CLIENT USERNAME>\AppData\Local\Astro\Saved\Paks", @"C:\Program Files (x86)\Steam\steamapps\common\ASTRONEER\Astro\Content\Paks"
        {
            Directory.CreateDirectory(paksPath);
            string[] files = Directory.GetFiles(paksPath, "*_P.pak", SearchOption.TopDirectoryOnly);

            string[] realPakPaths = Directory.GetFiles(installPath, "*.pak", SearchOption.TopDirectoryOnly);
            if (realPakPaths.Length == 0) throw new FileNotFoundException("Failed to locate any game installation pak files");
            string realPakPath = Directory.GetFiles(installPath, "Astro-*.pak", SearchOption.TopDirectoryOnly)[0];

            InitializeSearch(paksPath);

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

                    if (us == null || IntegratorUtils.IgnoredModIDs.Contains(us.ModID)) continue;
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

            CreatedPakData = new Dictionary<string, byte[]>
            {
                { "metadata.json", StarterPakData["metadata.json"] }
            };

            if (modCount > 0)
            {
                // Apply static files
                CreatedPakData = StarterPakData.ToDictionary(entry => entry.Key, entry => (byte[])entry.Value.Clone());

                if (!newComponents.ContainsKey("/Game/Globals/PlayControllerInstance")) newComponents.Add("/Game/Globals/PlayControllerInstance", new List<string>());
                newComponents["/Game/Globals/PlayControllerInstance"].Add("/Game/Integrator/ServerModComponent");

                // Generate mods data table
                var dtb = new DataTableBaker(this);
                IntegratorUtils.SplitExportFiles(dtb.Bake(allMods.ToArray(), OptionalModIDs, IntegratorUtils.Concatenate(CreatedPakData["Astro/Content/Integrator/ListOfMods.uasset"], CreatedPakData["Astro/Content/Integrator/ListOfMods.uexp"])), "Astro/Content/Integrator/ListOfMods.uasset", CreatedPakData);
                IntegratorUtils.SplitExportFiles(dtb.Bake2(IntegratorUtils.Concatenate(CreatedPakData["Astro/Content/Integrator/IntegratorStatics.uasset"], CreatedPakData["Astro/Content/Integrator/IntegratorStatics.uexp"])), "Astro/Content/Integrator/IntegratorStatics.uasset", CreatedPakData);
            }

            using (FileStream f = new FileStream(realPakPath, FileMode.Open, FileAccess.Read))
            {
                PakExtractor ourExtractor = null;
                try
                {
                    ourExtractor = new PakExtractor(new BinaryReader(f));
                }
                catch { }

                if (ourExtractor != null)
                {
                    // See if we can find the current version in this pak
                    byte[] defaultGameIni = ourExtractor.ReadRaw("Astro/Config/DefaultGame.ini");
                    if (defaultGameIni != null && defaultGameIni.Length > 0)
                    {
                        string iniIndicatedVersionStr = IniParser.FindLine(Encoding.UTF8.GetString(defaultGameIni), "/Script/EngineSettings.GeneralProjectSettings", "ProjectVersion");
                        if (iniIndicatedVersionStr != null) Version.TryParse(iniIndicatedVersionStr, out DetectedAstroBuild);
                    }

                    var actorBaker = new ActorBaker();
                    var itemListBaker = new ItemListBaker();
                    var levelBaker = new LevelBaker(ourExtractor, this);

                    // Patch level for persistent actors and missions
                    if (newPersistentActors.Count > 0 || newTrailheads.Count > 0)
                    {
                        foreach (string mapPath in MapPaths)
                        {
                            byte[] mapPathData1 = FindFile(mapPath, ourExtractor);
                            byte[] mapPathData2 = FindFile(Path.ChangeExtension(mapPath, ".uexp"), ourExtractor) ?? new byte[0];
                            if (mapPathData1 != null) IntegratorUtils.SplitExportFiles(levelBaker.Bake(newPersistentActors.ToArray(), newTrailheads.ToArray(), IntegratorUtils.Concatenate(mapPathData1, mapPathData2)), mapPath, CreatedPakData);
                        }
                    }

                    // Add components
                    foreach (KeyValuePair<string, List<string>> entry in newComponents)
                    {
                        string establishedPath = entry.Key.ConvertGamePathToAbsolutePath();

                        byte[] actorData1 = FindFile(establishedPath, ourExtractor);
                        byte[] actorData2 = FindFile(Path.ChangeExtension(establishedPath, ".uexp"), ourExtractor) ?? new byte[0];
                        if (actorData1 == null) continue;
                        try
                        {
                            IntegratorUtils.SplitExportFiles(actorBaker.Bake(entry.Value.ToArray(), IntegratorUtils.Concatenate(actorData1, actorData2)), establishedPath, CreatedPakData);
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

                        byte[] actorData1 = FindFile(establishedPath, ourExtractor);
                        byte[] actorData2 = FindFile(Path.ChangeExtension(establishedPath, ".uexp"), ourExtractor) ?? new byte[0];
                        if (actorData1 == null) continue;
                        try
                        {
                            IntegratorUtils.SplitExportFiles(itemListBaker.Bake(entry.Value, IntegratorUtils.Concatenate(actorData1, actorData2)), establishedPath, CreatedPakData);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(ex.ToString());
                        }
                    }
                }
            }

            byte[] pakData = PakBaker.Bake(CreatedPakData);

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
