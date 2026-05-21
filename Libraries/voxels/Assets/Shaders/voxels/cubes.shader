HEADER
{
    Description = "Renders voxels as cubes";
    DevShader = true;
}

FEATURES
{
    #include "common/features.hlsl"
}

MODES
{
    Forward();
    Depth();
}

COMMON
{
    #include "common/shared.hlsl"
    #include "Shaders/marching_cubes/common.hlsl"
}

struct VertexInput
{
    #include "common/vertexinput.hlsl"
};

struct PixelInput
{
    #include "common/pixelinput.hlsl"

    float3 vChunkLocal : TEXCOORD5;
};

VS
{
    #include "common/vertex.hlsl"

    float3 WorldOrigin < Attribute("WorldOrigin"); > ;
    float3 WorldSize < Attribute("WorldSize"); > ;

    PixelInput MainVs(RenderVertex v)
    {
        VertexInput i;

        i.vPositionOs = v.Position + WorldOrigin;
        i.vNormalOs = float4(v.Normal, 0);

        PixelInput o = ProcessVertex(i);

        o = FinalizeVertex(o);

        o.vChunkLocal = (v.Position / WorldSize);

        return o;
    }
}

PS
{
    #include "common/pixel.hlsl"
    #include "Shaders/simplex3d.hlsl"

    float3 Tint < Attribute("Tint"); > ;

    uint LodMask < Attribute("LodMask"); > ;
    float LodDistance < Attribute("LodDistance"); > ;

    float4 MainPs(PixelInput i) : SV_Target0
    {
        uint3 octants = uint3(i.vChunkLocal * 2);
        uint octantIndex = dot(octants, uint3(1, 2, 4));

        float lodLoaded = (LodMask >> octantIndex) & 1;

        clip(0.5 - lodLoaded);

        Material m = Material::From(i);

        float3 voxel = trunc(i.vChunkLocal * 32.0) / 4.0;
        float tweak = SimplexNoise3D(voxel) * 0.125 + 0.75;

        m.Albedo *= Tint * tweak;

        return ShadingModelStandard::Shade(m);
    }
}
