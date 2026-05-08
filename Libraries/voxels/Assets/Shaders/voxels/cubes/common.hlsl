enum CubeNormal
{
    NEG_X,
    POS_X,

    NEG_Y,
    POS_Y,

    NEG_Z,
    POS_Z
};

enum QuadCorner
{
    X0_Y0,
    X1_Y0,

    X0_Y1,
    X1_Y1
};

struct CubeFace
{
    int3 Position;
    int Normal;
};

struct CubeVertex
{
    float3 Position : POSITION < Semantic(PosXyz); >;
    float3 Normal : NORMAL;
    float4 Tangent : TANGENT < Semantic(TangentU_SignV); >;
    float2 TexCoord : TEXCOORD0;
};

struct CubeFaceBasis
{
    int3 Normal;
    int3 TangentU;
    int3 TangentV;
};

static const CubeFaceBasis CubeFaceBases[6] =
{
    { float3(-1, 0, 0), float3(0, -1, 0), float3(0, 0, +1) },
    { float3(+1, 0, 0), float3(0, +1, 0), float3(0, 0, +1) },

    { float3(0, -1, 0), float3(+1, 0, 0), float3(0, 0, +1) },
    { float3(0, +1, 0), float3(-1, 0, 0), float3(0, 0, +1) },

    { float3(0, 0, -1), float3(+1, 0, 0), float3(0, -1, 0) },
    { float3(0, 0, +1), float3(+1, 0, 0), float3(0, +1, 0) }
};

static const float3 CubeFaceOffsets[6] =
{
    float3(0, 1, 0),
    float3(1, 0, 0),

    float3(0, 0, 0),
    float3(1, 1, 0),

    float3(0, 1, 0),
    float3(0, 0, 1)
};
