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

    struct CompressedFace
    {
        uint PackedVertices[6];
    };

    StructuredBuffer<Voxel> VoxelData < Attribute("VoxelData"); >;
    uint3 VoxelOffset < Attribute("VoxelOffset"); >;
    uint2 VoxelStride < Attribute("VoxelStride"); >;

    AppendStructuredBuffer<CompressedFace> FaceBuffer < Attribute("FaceBuffer"); >;

    Voxel GetVoxel(uint3 index)
    {
        return VoxelData[index.x + VoxelStride.x * index.y + VoxelStride.y * index.z];
    }

    uint PackVertex(uint3 position, CubeFace face, uint2 texCoord)
    {
        return (position.x & 0xff)
            | ((position.y & 0xff) << 8)
            | ((position.z & 0xff) << 16)
	        | ((uint(face) & 0x7) << 24)
            | ((texCoord.x & 0x1) << 27)
            | ((texCoord.y & 0x1) << 28);
    }

    void AppendFace(uint3 origin, CubeFace face)
    {
        uint3 faceOrigin = origin + CubeFaceOffsets[face];
        CubeFaceBasis basis = CubeFaceBases[face];

        uint v00 = PackVertex(faceOrigin, face, uint2(0, 0));
        uint v10 = PackVertex(faceOrigin + basis.TangentU, face, uint2(1, 0));
        uint v01 = PackVertex(faceOrigin + basis.TangentV, face, uint2(0, 1));
        uint v11 = PackVertex(faceOrigin + basis.TangentU + basis.TangentV, face, uint2(1, 1));

        CompressedFace o;

        o.PackedVertices[0] = v00;
        o.PackedVertices[1] = v10;
        o.PackedVertices[2] = v01;

        o.PackedVertices[3] = v01;
        o.PackedVertices[4] = v10;
        o.PackedVertices[5] = v11;

        FaceBuffer.Append(o);
    }

    [numthreads( 1, 1, 1 )]
    void MainCs(uint3 dispatchId : SV_DispatchThreadID)
    {
        uint3 index = VoxelOffset + dispatchId;

        Voxel voxel = GetVoxel(index);

        if (voxel.Value == 0) return;

        if (GetVoxel(index - int3(1, 0, 0)).Value == 0) AppendFace(dispatchId, CubeFace::NEG_X);
        if (GetVoxel(index + int3(1, 0, 0)).Value == 0) AppendFace(dispatchId, CubeFace::POS_X);

        if (GetVoxel(index - int3(0, 1, 0)).Value == 0) AppendFace(dispatchId, CubeFace::NEG_Y);
        if (GetVoxel(index + int3(0, 1, 0)).Value == 0) AppendFace(dispatchId, CubeFace::POS_Y);

        if (GetVoxel(index - int3(0, 0, 1)).Value == 0) AppendFace(dispatchId, CubeFace::NEG_Z);
        if (GetVoxel(index + int3(0, 0, 1)).Value == 0) AppendFace(dispatchId, CubeFace::POS_Z);
    }
}
