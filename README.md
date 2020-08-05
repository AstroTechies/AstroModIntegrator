# AstroModIntegrator
Integrates Astroneer mods based on their metadata.json files in order to avoid mod conflict.

## Sample Code
```cs
Stopwatch stopWatch = new Stopwatch();
stopWatch.Start();

// Scan through all pak files in the first parameter, reads their metadata.json file, attaches components to the specified actors as needed from the pak in the second parameter, then saves a new mod called 999-AstroModLoader_P.pak in the first parameter
ModIntegrator.IntegrateMods(Path.Combine(Environment.GetEnvironmentVariable("LocalAppData"), @"Astro\Saved\Paks"), @"C:\Program Files (x86)\Steam\steamapps\common\ASTRONEER\Astro\Content\Paks");

stopWatch.Stop();
TimeSpan ts = stopWatch.Elapsed;
Console.WriteLine("Done, " + ((double)ts.Ticks / TimeSpan.TicksPerMillisecond) + " ms in total");
```