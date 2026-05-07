enum CubeFace
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

struct CubeFaceBasis
{
    float3 Normal;
    float3 TangentU;
    float3 TangentV;
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

/*
struct CubeVertex
{
    uint Packed;

    __init(uint3 position, CubeFace face, QuadCorner corner)
    {
        Packed = ((position.x & 0xff) << 24) | ((position.y & 0xff) << 16) | ((position.z & 0xff) << 8) | ((face & 0x7) << 2) | (corner & 0x3);
    }
};
*/