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
    #include "Shaders/voxels/cubes_common.hlsl"
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

    struct CompressedVertexInput
    {
        uint Packed : TEXCOORD0;

        uint3 GetPosition()
        {
            return uint3(Packed & 0xff, (Packed >> 8) & 0xff, (Packed >> 16) & 0xff);
        }

        uint GetFace()
        {
            return (Packed >> 24) & 0x7;
        }

        uint GetCorner()
        {
            return (Packed >> 27) & 0x3;
        }

        int2 GetTexCoord()
        {
            uint corner = GetCorner();

            return int2(corner & 1, (int(corner) >> 1) & 1);
        }

        CubeFaceBasis GetBasis()
        {
            return CubeFaceBases[GetFace()];
        }
    };

    VertexInput DecompressVertex(CompressedVertexInput i)
    {
        float2 texCoord = i.GetTexCoord();
        CubeFaceBasis basis = i.GetBasis();

        VertexInput o;

        o.vPositionOs = i.GetPosition() * 32.0;
        o.vTexCoord = texCoord;
        o.vNormalOs = float4(basis.Normal, 0);
        o.vTangentUOs_flTangentVSign = float4(basis.TangentU, 1);

        return o;
    }

    PixelInput MainVs(CompressedVertexInput compressed)
    {
        VertexInput i = DecompressVertex(compressed);
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
