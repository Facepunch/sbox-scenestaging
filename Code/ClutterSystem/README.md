# Clutter System - Isotope Resource

A simple, efficient weighted asset selection system for procedural clutter placement in s&box.

## Overview

The **Clutter Isotope** is a GameResource that defines weighted collections of Prefabs and Models. It provides a foundation for any system that needs to randomly select from a pool of variants based on probability.

### Why "Isotope"?
In chemistry, isotopes are variants of the same element with different properties. Similarly, a Clutter Isotope contains variants of similar objects (e.g., different rock models, grass types) that can be randomly selected with configurable probabilities.

---

## Components

### ?? **IsotopeEntry**
Individual weighted item containing:
- **Prefab** or **Model** reference
- **Weight** for random selection (0.01 - 1.0)
- Helpers: `HasAsset`, `AssetName`

### ?? **ClutterIsotope** (GameResource)
Weighted collection of entries with:
- `List<IsotopeEntry>` - Collection of weighted entries
- `UniformDistribution` - Toggle between weighted/equal probability
- `ValidEntryCount` - Number of usable entries
- `GetRandomEntry()` - Select one random entry
- `GetRandomEntries(count, allowDuplicates)` - Select multiple entries
- `GetWeightDistributionSummary()` - Debug string showing percentages
- `Validate()` - Check for configuration errors

---

## Usage

### 1. Create an Isotope Resource
1. Right-click in Asset Browser ? **Create** ? **Clutter Isotope** (`.isotope`)
2. Name it (e.g., `forest_rocks.isotope`)

### 2. Configure Entries
Add entries with different weights:

```
Entry 1: small_rock.vmdl    Weight: 0.70  (70% chance)
Entry 2: medium_rock.vmdl   Weight: 0.20  (20% chance)
Entry 3: large_rock.vmdl    Weight: 0.10  (10% chance)
```

**Uniform Distribution**: Enable to ignore weights and pick randomly with equal probability.

### 3. Use in Code

```csharp
// Load the isotope resource
var isotope = ResourceLibrary.Get<ClutterIsotope>( "materials/clutter/forest_rocks.isotope" );

// Get a single random entry
var entry = isotope.GetRandomEntry();
if ( entry is not null )
{
    // Spawn the prefab or instantiate the model
    var spawned = entry.Prefab?.Clone( position, rotation );
}

// Get multiple entries at once
var entries = isotope.GetRandomEntries( 50, allowDuplicates: true );
foreach ( var e in entries )
{
    // Batch spawn logic here
}

// Debug weight distribution
Log.Info( isotope.GetWeightDistributionSummary() );
```

---

## API Reference

### ClutterIsotope Methods

#### `IsotopeEntry GetRandomEntry()`
Returns a random entry based on weights. Returns `null` if no valid entries exist.

#### `List<IsotopeEntry> GetRandomEntries(int count, bool allowDuplicates = true)`
Returns multiple entries. If `allowDuplicates` is false, each entry can only be picked once.

#### `string GetWeightDistributionSummary()`
Returns formatted string showing probability percentages for each entry.

#### `List<string> Validate()`
Checks configuration and returns list of warning/error messages.

### Properties

- `Entries` - List of weighted entries
- `UniformDistribution` - If true, ignores weights and picks equally
- `ValidEntryCount` - Number of entries with assets and weight > 0 (read-only)

---

## Design Philosophy

### ? **Simple & Focused**
- Single responsibility: weighted asset selection
- No spawning logic (that's for your systems to implement)
- No placement rules, density, or rendering optimizations
- Pure data resource

### ?? **Composable**
- Foundation for painters, layers, generators, scatters
- Works with both Prefabs and Models
- Easy to integrate into any system

### ?? **Fluent API**
```csharp
// Clean, expressive usage
myIsotope
    .GetRandomEntries(100)
    .Select(e => e.Prefab)
    .Where(p => p != null)
    .ForEach(prefab => SpawnSomewhere(prefab));
```

---

## Weight Calculation

### Weighted Mode (Default)
Weights are normalized to probabilities:
```
Entry A: Weight 10 ? 10/(10+5+1) = 62.5% chance
Entry B: Weight 5  ? 5/(10+5+1)  = 31.25% chance  
Entry C: Weight 1  ? 1/(10+5+1)  = 6.25% chance
```

### Uniform Mode
All entries have equal probability regardless of weight:
```
3 entries = 33.33% each
```

---

## Future Systems (Not Implemented)

The Isotope is intentionally minimal. Future systems that will **consume** it:

- **ClutterPainter**: Manual painting component
- **ClutterLayer**: Density-based procedural placement
- **ClutterBiome**: Multiple isotopes with blend masks
- **ClutterScatter**: Noise-driven distribution

The Isotope provides the "what to spawn" - other systems provide the "where and how".

---

## Example Use Cases

### Forest Rocks
```
Small rocks (70%), Medium (20%), Large (10%)
Use for natural-looking rock scatter
```

### Grass Varieties
```
Grass01, Grass02, Grass03
Enable Uniform Distribution for equal variety
```

### Loot Drops
```
Common item (0.7), Uncommon (0.2), Rare (0.08), Epic (0.02)
Weighted for typical loot table behavior
```

---

## Validation

The `Validate()` method checks for:
- Empty entry list
- Entries with no Prefab/Model assigned
- Entries with weight <= 0
- No valid entries overall

Use in editor tools or OnValidate() to catch configuration errors.

---

## Files

```
Code/ClutterSystem/
??? IsotopeEntry.cs        # Weighted entry data class
??? ClutterIsotope.cs      # GameResource definition
??? README.md              # This file
```

---

## Inspiration

- **Unreal Engine**: Foliage Type (asset definition)
- **Unity**: Detail Prototype (terrain asset)
- **Houdini**: Weighted Instance Selection

---

## Notes

- **Prefab Priority**: If both Prefab and Model are set, code should use Prefab
- **Weight = 0**: Entry is automatically skipped
- **Null Entries**: Automatically cleaned on PostLoad()
- **Thread Safety**: Use on main thread only (uses `Game.Random`)

---

**Simple resource, infinite possibilities! Build your spawners, painters, and scatters on top of this foundation.** ??
