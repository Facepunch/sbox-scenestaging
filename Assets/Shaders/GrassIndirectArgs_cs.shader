MODES
{
    Default(); // Bullshit bullshit why do we still need this?
}


CS
{
    #include "system.fxc"

    struct DrawIndexedIndirectArgs
    {
        uint IndexCountPerInstance;
        uint InstanceCount;
        uint StartIndexLocation;
        uint BaseVertexLocation;
        uint StartInstanceLocation;
    };
    
    RWStructuredBuffer<DrawIndexedIndirectArgs> g_DrawArgs <Attribute( "IndirectDrawCommandBuffer" );>;
    RWByteAddressBuffer g_BladeCounter < Attribute( "BladeCounter" ); >; // for blade count
    
    uint IndexCountPerInstance < Attribute( "IndexCountPerInstance" ); >;   // set from GrassMesh.IndexCount

    [numthreads(1,1,1)]
    void MainCs( uint3 id : SV_DispatchThreadID )
    {
        DrawIndexedIndirectArgs args;
        args.IndexCountPerInstance = IndexCountPerInstance;
        args.InstanceCount = g_BladeCounter.Load(0);
        args.StartIndexLocation = 0;
        args.BaseVertexLocation = 0;
        args.StartInstanceLocation = 0;

        // Reset the counter
        g_BladeCounter.Store(0, 0);
        
        g_DrawArgs[0] = args;
    }
}