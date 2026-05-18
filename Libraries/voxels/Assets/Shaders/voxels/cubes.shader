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
};

VS
{
    #include "common/vertex.hlsl"

    float3 WorldOrigin < Attribute("WorldOrigin"); > ;
    float VoxelScale < Attribute("VoxelScale"); > ;

    PixelInput MainVs(RenderVertex v)
    {
        VertexInput i;

        i.vPositionOs = v.Position + WorldOrigin;
        i.vNormalOs = float4(v.Normal, 0);

        PixelInput o = ProcessVertex(i);
        return FinalizeVertex( o );
    }
}

PS
{
    #include "common/pixel.hlsl"

    float4 MainPs(PixelInput i) : SV_Target0
    {
        Material m = Material::From(i);

        return ShadingModelStandard::Shade(m);
    }
}
