# AstroModIntegrator
Integrates Astroneer mods based on their metadata.json files in order to avoid mod conflict.

## Sample Code
```cs
Stopwatch stopWatch = new Stopwatch();
stopWatch.Start();

new ModIntegrator()
{
    IsServer = false,
    RefuseVanillaConnections = true
}.IntegrateMods(Path.Combine(Environment.GetEnvironmentVariable("LocalAppData"), @"Astro\Saved\Paks"), @"C:\Program Files (x86)\Steam\steamapps\common\ASTRONEER\Astro\Content\Paks");

stopWatch.Stop();
TimeSpan ts = stopWatch.Elapsed;
Console.WriteLine("Done, " + ((double)ts.Ticks / TimeSpan.TicksPerMillisecond) + " ms in total");
```