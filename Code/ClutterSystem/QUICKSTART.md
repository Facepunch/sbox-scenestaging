# Clutter Isotope - Quick Reference

## What is it?
A **GameResource** (`.isotope` file) that holds a weighted list of Prefabs/Models for random selection.

## Files Created
- `Code/ClutterSystem/IsotopeEntry.cs` - Data class for each entry
- `Code/ClutterSystem/ClutterIsotope.cs` - Main GameResource
- `Code/ClutterSystem/README.md` - Full documentation

## Basic Structure

```csharp
ClutterIsotope
?? Entries (List<IsotopeEntry>)
?  ?? Prefab (GameObject)
?  ?? Model (Model)
?  ?? Weight (float)
?? UniformDistribution (bool)
```

## Core API

```csharp
// Get one random entry
var entry = isotope.GetRandomEntry();

// Get multiple
var entries = isotope.GetRandomEntries(10, allowDuplicates: true);

// Debug info
Log.Info(isotope.GetWeightDistributionSummary());

// Validation
var warnings = isotope.Validate();
```

## Usage Pattern

1. **Create** `.isotope` file in asset browser
2. **Add entries** with Prefabs/Models and weights
3. **Load** in code: `ResourceLibrary.Get<ClutterIsotope>("path")`
4. **Select** random entries: `GetRandomEntry()`
5. **Spawn** yourself however you want

## Key Design Points

? **Just the resource** - No spawning, no placement, no rendering  
? **Pure selection** - Weighted random picking from a pool  
? **Your rules** - Build painters/scatters/generators on top  
? **Simple** - 2 classes, ~200 lines total  

## What's NOT included

? No spawning components  
? No placement/scatter logic  
? No density/noise systems  
? No LOD/instancing/culling  

*This is the foundation. Build your systems on top!*

## Next Steps

When ready to build spawning systems:
- Create a `ClutterPainter` component that uses the isotope
- Create a `ClutterLayer` for procedural placement
- Create a `ClutterScatter` for noise-based distribution

The isotope provides **"what to spawn"** - your systems provide **"where and how"**.
