MODES
{
    Default(); // Bullshit bullshit why do we still need this?
}

CS
{
    #include "common/shared.hlsl"
    #include "common/Bindless.hlsl"
    #include "common/classes/Depth.hlsl"

    #include "GrassCommon.hlsl"
    #include "WindCommon.hlsl"
    
    #include "terrain/TerrainCommon.hlsl"
    #include "terrain/TerrainClipmap.hlsl"

    RWStructuredBuffer<Blade> g_OutBlades < Attribute("BladeBuffer"); > ;
    RWByteAddressBuffer g_BladeCounter < Attribute("BladeCounter"); > ;

    // Parameters set from C#
	float2 PatchMin < Attribute("PatchMin"); >; // XY world bounds
	float2 PatchMax < Attribute("PatchMax"); >;
	uint NumBladesWanted < Attribute("NumBladesWanted"); >; // threads that should emit
	float Time < Attribute("Time"); >;           // for swaying noise

    // Cheap hash helpers ---------------------------------------------------------
    uint Hash(uint n)
    {
        n ^= n >> 16;
        n *= 0x7feb352d;
        n ^= n >> 15;
        n *= 0x846ca68b;
        n ^= n >> 16;
        return n;
    }
    float Hash01(uint n)
    {
        return (Hash(n) & 0x00FFFFFFu) / 16777215.0;
    }

    float2 Hash02(uint n)
    {
        uint h = Hash(n);
        return float2((h & 0xFFFFu), (h >> 16)) / 65535.0;
    }

    float3 SampleWind(float3 pos)
    {
        float strength = 1.0f;
        float stiffness = 0.0f;
        float3 wind = Wind::GetWindDisplacement( pos, strength, stiffness );
        return wind;
    }

    bool PositionVisibleAt(float3 pos)
    {
         // Todo: doesn't check for Bbox
        // Convert world position to projection space
        float4 clipPos = Position3WsToPs(pos);
        clipPos.xyz /= clipPos.w; // perspective divide

        // Check if visible on screen
        if( clipPos.x < -1.0f || clipPos.x > 1.0f ||
            clipPos.y < -1.0f || clipPos.y > 1.0f ||
            clipPos.z < 0.0f || clipPos.z > 1.0f )
        {
            return false;
        }

        // Depth test
        float2 uv = clipPos.xy * 0.5f + 0.5f; // [0,1] range
        uv.y = 1.0f - uv.y; // flip Y for texture coords
        
        float flDepth = Depth::Normalize( g_tDepthChain.SampleLevel( g_sPointWrap, uv, 5.0f ).x ) ;

        if(flDepth > clipPos.z)
        {
            return false; // culled by depth
        }

        return true;
	}

    Blade GenerateGrassBlade(uint idx, out bool isVisible)
    {
        isVisible = false;

        // ----- random values -----------------------------------------------------
        uint seed = idx * 9781u + 231u; // any large coprime numbers
        float2 r2 = Hash02(seed);
        float rHeight = Hash01(seed * 13u);
        float rWidth = Hash01(seed * 37u);
        float rTilt = Hash01(seed * 71u);
        float rBend = Hash01(seed * 97u);
        float rCurve = Hash01(seed * 101u);

        // ----- world‑space placement --------------------------------------------
        float3 pos = 0;
        pos.xy = lerp(PatchMin, PatchMax, r2); // random point in rect

        float3 terrainPos = mul(Terrain::Get().TransformInv, float4(pos.xyz, 1.0)).xyz;
        pos.z = Terrain::GetHeight(terrainPos.xy); // height at this point

        // ---- GPU Culling  -----------------------------------------------------
        Blade b = (Blade)0;
        if (!PositionVisibleAt(pos))
            return b;

        // ----- terrain splatmap -----------------------------------------------------
        int splat = 3;
        float4 control = Terrain::GetControl(terrainPos.xy);
        float height = control[3];
        if (height < 0.5f)
            return b;


        // ---- albedo, color ----------------------------------------------
        Texture2D tAlbedo = Bindless::GetTexture2D(g_TerrainMaterials[splat].bcr_texid);
        
        float4 albedo = pow( tAlbedo.SampleLevel(g_sBilinearWrap, pos.xy / 1024, 2.0f), 2.2f);

        // ----- vectors (Ghost of Tsushima clustering approach) -------------------
        float facingAngle = Hash02(seed * 128u).x * 6.28318; // 0‑2π
        float2 facing = float2(cos(facingAngle), sin(facingAngle));
        
        // Add clumping behavior - grass tends to grow in similar directions locally
        float2 clumpCenter = floor(pos.xy / 2.0) * 2.0; // 2m clumps
        uint clumpSeed = Hash(uint(clumpCenter.x * 1000 + clumpCenter.y));
        float clumpAngle = Hash01(clumpSeed) * 6.28318;
        float2 clumpFacing = float2(cos(clumpAngle), sin(clumpAngle));
        
        // Blend individual and clump facing (more clumped look)
        float clumpStrength = 0.4; // How much grass follows clump direction
        facing = normalize(lerp(facing, clumpFacing, clumpStrength));

        // ----- assemble struct ---------------------------------------------------
        b.Position = pos;
        b.Facing = facing;
        b.Wind = SampleWind(pos) * 50;
        b.Hash = seed;
        b.GrassType = 0;
        b.ClumpFacing = clumpFacing;
        b.Color = albedo.xyz;
        
        // Ghost of Tsushima style parameters (keeping original height)
        b.Height = lerp(0.5, 1.0, rHeight) * height;
        b.Width = lerp(1.5, 3.0, rWidth);
        b.Tilt = lerp(-12.0, 12.0, rTilt) * (1.0 - height * 0.5); // More tilt in sparse areas
        b.Bend = rBend * rBend; // Quadratic bend for more natural distribution
        b.SideCurve = (rCurve - 0.5) * 2.0 * saturate(rHeight * 2.0); // More curve on taller grass

        isVisible = true;
        return b;
    }

    
    // Main -------------------------------------------------------------------------

    #define NUM_THREADS 64

    // Needed as InterlockedAdd isn't thread group safe
    groupshared uint  gWriteSize[NUM_THREADS];
    groupshared uint  gWriteOffset[NUM_THREADS];
    groupshared uint  gGroupBase;

    [numthreads(NUM_THREADS, 1, 1)]
    void MainCs(uint3 id: SV_DispatchThreadID,
                uint3 groupThreadID: SV_GroupThreadID,
                uint3 groupID: SV_GroupID)
    {
        const uint lane = groupThreadID.x;
        const bool isValid = (id.x < NumBladesWanted);

        // Generate and decide visibility (only if valid)
        bool isVisible = false;
        Blade blade = (Blade)0;
        if (isValid)
        {
            blade = GenerateGrassBlade(id.x, isVisible);
        }

        // Each thread contributes 1 if visible, else 0
        gWriteSize[lane] = (isValid && isVisible) ? 1u : 0u;
        GroupMemoryBarrierWithGroupSync();

        // Join logic 
        if (lane == 0)
        {
            uint run = 0u;
            [unroll]
            for (uint i = 0; i < NUM_THREADS; ++i)
            {
                gWriteOffset[i] = run;
                run += gWriteSize[i];
            }

            // Reserve a contiguous block for the whole group with ONE atomic
            const uint totalGroup = gWriteOffset[NUM_THREADS - 1] + gWriteSize[NUM_THREADS - 1];

            g_BladeCounter.InterlockedAdd(0, totalGroup, gGroupBase);
        }
        GroupMemoryBarrierWithGroupSync();

        // Write only if visible into tightly-packed region
        if (isValid && isVisible)
        {
            const uint outIndex = gGroupBase + gWriteOffset[lane];
            g_OutBlades[outIndex] = blade;
        }
    }
}
