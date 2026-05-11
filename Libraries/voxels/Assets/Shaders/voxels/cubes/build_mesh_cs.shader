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

    StructuredBuffer<CubeFace> FaceBuffer < Attribute("FaceBuffer"); > ;
    uint FirstFaceIndex < Attribute("FirstFaceIndex"); > ;
    RWStructuredBuffer<CubeVertex> VertexBuffer < Attribute("VertexBuffer"); > ;
    RWStructuredBuffer<uint> IndexBuffer < Attribute("IndexBuffer"); > ;

    [numthreads( 1, 1, 1 )]
    void MainCs(uint dispatchId : SV_DispatchThreadID)
    {
        CubeFace face = FaceBuffer[dispatchId + FirstFaceIndex];
        CubeFaceBasis basis = CubeFaceBases[face.Normal];

        CubeVertex v00;
        CubeVertex v01;
        CubeVertex v10;
        CubeVertex v11;

        float3 basePosition = face.Position + CubeFaceOffsets[face.Normal];

        v00.Position = basePosition;
        v01.Position = basePosition + basis.TangentU;
        v10.Position = basePosition + basis.TangentV;
        v11.Position = basePosition + basis.TangentU + basis.TangentV;

        v00.TexCoord = float2(0, 0);
        v01.TexCoord = float2(0, 1);
        v10.TexCoord = float2(1, 0);
        v11.TexCoord = float2(1, 1);

        v00.Normal = v01.Normal = v10.Normal = v11.Normal = basis.Normal;
        v00.Tangent = v01.Tangent = v10.Tangent = v11.Tangent = float4(basis.TangentU, 1);

        uint vertexOffset = dispatchId * 4;

        VertexBuffer[vertexOffset + 0] = v00;
        VertexBuffer[vertexOffset + 1] = v01;
        VertexBuffer[vertexOffset + 2] = v10;
        VertexBuffer[vertexOffset + 3] = v11;

        uint indexOffset = dispatchId * 6;

        IndexBuffer[indexOffset + 0] = vertexOffset + 0;
        IndexBuffer[indexOffset + 1] = vertexOffset + 1;
        IndexBuffer[indexOffset + 2] = vertexOffset + 2;

        IndexBuffer[indexOffset + 3] = vertexOffset + 1;
        IndexBuffer[indexOffset + 4] = vertexOffset + 3;
        IndexBuffer[indexOffset + 5] = vertexOffset + 2;
    }
}
