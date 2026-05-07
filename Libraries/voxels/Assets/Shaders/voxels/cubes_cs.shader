MODES
{
    Default();
}

CS
{
    #include "Shaders/voxels/cubes_common.hlsl"

    struct Voxel
    {
        uint Value;
    };

    StructuredBuffer<Voxel> VoxelData < Attribute("VoxelData"); >;
    uint3 VoxelOffset < Attribute("VoxelOffset"); >;
    uint2 VoxelStride < Attribute("VoxelStride"); >;

    AppendStructuredBuffer<Face> FaceBuffer < Attribute("FaceBuffer"); >;

    Voxel GetVoxel(uint3 index)
    {
        return VoxelData[index.x + VoxelStride.x * index.y + VoxelStride.y * index.z];
    }

    void AppendFace(uint3 origin, Axis axis)
    {
        Face face;

        face.Origin = origin;
        face.Axis = axis;

        FaceBuffer.Append(face);
    }

    [numthreads( 1, 1, 1 )]
    void MainCs(uint3 dispatchId : SV_DispatchThreadID)
    {
        uint3 index = VoxelOffset + dispatchId;

        Voxel voxel = GetVoxel(index);

        if (voxel.Value == 0) return;

        if (GetVoxel(index - int3(1, 0, 0)).Value == 0) AppendFace(dispatchId, Axis::NEG_X);
        if (GetVoxel(index + int3(1, 0, 0)).Value == 0) AppendFace(dispatchId, Axis::POS_X);

        if (GetVoxel(index - int3(0, 1, 0)).Value == 0) AppendFace(dispatchId, Axis::NEG_Y);
        if (GetVoxel(index + int3(0, 1, 0)).Value == 0) AppendFace(dispatchId, Axis::POS_Y);

        if (GetVoxel(index - int3(0, 0, 1)).Value == 0) AppendFace(dispatchId, Axis::NEG_Z);
        if (GetVoxel(index + int3(0, 0, 1)).Value == 0) AppendFace(dispatchId, Axis::POS_Z);
    }
}
