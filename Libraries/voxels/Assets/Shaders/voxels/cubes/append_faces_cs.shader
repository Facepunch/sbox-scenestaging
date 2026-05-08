MODES
{
    Default();
}

CS
{
    #include "Shaders/voxels/cubes/common.hlsl"

    struct Voxel
    {
        uint Value;
    };

    StructuredBuffer<Voxel> VoxelData < Attribute("VoxelData"); >;
    uint3 VoxelOffset < Attribute("VoxelOffset"); >;
    uint2 VoxelStride < Attribute("VoxelStride"); >;

    AppendStructuredBuffer<CubeFace> FaceBuffer < Attribute("FaceBuffer"); >;

    Voxel GetVoxel(uint3 index)
    {
        return VoxelData[index.x + VoxelStride.x * index.y + VoxelStride.y * index.z];
    }

    void AppendFace(int3 position, CubeNormal normal)
    {
        CubeFace face;

        face.Position = position;
        face.Normal = int(normal);

        FaceBuffer.Append(face);
    }

    [numthreads( 1, 1, 1 )]
    void MainCs(uint3 dispatchId : SV_DispatchThreadID)
    {
        uint3 index = VoxelOffset + dispatchId;

        Voxel voxel = GetVoxel(index);

        if (voxel.Value == 0) return;

        if (GetVoxel(index - int3(1, 0, 0)).Value == 0) AppendFace(dispatchId, CubeNormal::NEG_X);
        if (GetVoxel(index + int3(1, 0, 0)).Value == 0) AppendFace(dispatchId, CubeNormal::POS_X);

        if (GetVoxel(index - int3(0, 1, 0)).Value == 0) AppendFace(dispatchId, CubeNormal::NEG_Y);
        if (GetVoxel(index + int3(0, 1, 0)).Value == 0) AppendFace(dispatchId, CubeNormal::POS_Y);

        if (GetVoxel(index - int3(0, 0, 1)).Value == 0) AppendFace(dispatchId, CubeNormal::NEG_Z);
        if (GetVoxel(index + int3(0, 0, 1)).Value == 0) AppendFace(dispatchId, CubeNormal::POS_Z);
    }
}
