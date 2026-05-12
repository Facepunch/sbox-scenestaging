struct Voxel
{
    uint Value;
};

// Z = 0   Z = 1
// c - d   g - h
// |   |   |   |
// a - b   e - f
enum
{
    CubeVertices_None = 0,
    CubeVertices_All = 255,

    CubeVertices_A = 1,
    CubeVertices_B = 2,
    CubeVertices_C = 4,
    CubeVertices_D = 8,
    CubeVertices_E = 16,
    CubeVertices_F = 32,
    CubeVertices_G = 64,
    CubeVertices_H = 128
};

enum
{
    CubeEdge_AB = 0,
    CubeEdge_AC = 1,
    CubeEdge_AE = 2
};

struct RenderVertex
{
    float3 Position : POSITION < Semantic(PosXyz); > ;
    float3 Normal : NORMAL;
};

StructuredBuffer<Voxel> VoxelData < Attribute("VoxelData"); > ;
uint3 VoxelCount < Attribute("VoxelCount"); > ;
uint3 VoxelOffset < Attribute("VoxelOffset"); > ;
uint2 VoxelStride < Attribute("VoxelStride"); > ;

uint VertexBufferOffset < Attribute("VertexBufferOffset"); > ;
uint2 VertexIndexMapStride < Attribute("VertexIndexMapStride"); > ;

RWStructuredBuffer<uint> ResultBuffer < Attribute("ResultBuffer"); > ;
uint ResultBufferOffset < Attribute("ResultBufferOffset"); > ;

uint GetVoxelIndex(int3 pos)
{
    pos = clamp(pos, 0, VoxelCount - 1);

    return pos.x + VoxelStride.x * pos.y + VoxelStride.y * pos.z;
}

uint GetVertexIndexMapIndex(int3 pos)
{
    return pos.x + VertexIndexMapStride.x * pos.y + VertexIndexMapStride.y * pos.z;
}

Voxel GetVoxel(int3 pos)
{
    return VoxelData[GetVoxelIndex(pos)];
}
