#ifndef GRASS_COMMON_HLSL
#define GRASS_COMMON_HLSL

struct Blade // must match C# layout 1‑for‑1
{
    float3 Position;    // world‑space
    float2 Facing;      // normalized 2‑D heading
    float3 Wind;         // sampled from noise/texture
    uint Hash;          // random seed for vertex shader
    uint GrassType;     // 0‑N switch in VS/PS
    float2 ClumpFacing; // average heading of clump
    float3 Color;       // tint picked in compute
    float Height;
    float Width;
    float Tilt;
    float Bend;
    float SideCurve;
};

StructuredBuffer<Blade> GrassBladeBuffer < Attribute("BladeBuffer"); > ;

#endif