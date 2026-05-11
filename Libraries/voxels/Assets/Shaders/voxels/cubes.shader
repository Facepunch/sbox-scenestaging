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
    #include "Shaders/voxels/cubes/common.hlsl"
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

    int3 WorldOrigin < Attribute("WorldOrigin"); > ;
    float VoxelSize < Attribute("VoxelSize"); > ;

    PixelInput MainVs(CubeVertex v)
    {
        VertexInput i;

        i.vPositionOs = (v.Position + WorldOrigin) * VoxelSize;
        i.vTexCoord = v.TexCoord;
        i.vNormalOs = float4(v.Normal, 0);
        i.vTangentUOs_flTangentVSign = v.Tangent;

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
