# AstroModIntegrator
Integrates Astroneer mods based on their metadata.json files in order to avoid mod conflict.

## Sample Code
```cs
ModIntegrator.IntegrateMods(Path.Combine(Environment.GetEnvironmentVariable("LocalAppData"), @"Astro\Saved\Paks"));
```