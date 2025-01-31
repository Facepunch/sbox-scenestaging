//-------------------------------------------------------------------------------------------------------------------------------------------------------------
HEADER
{
	DevShader = true;
	Description = "";
}

//-------------------------------------------------------------------------------------------------------------------------------------------------------------
MODES
{
	Default();
}

//-------------------------------------------------------------------------------------------------------------------------------------------------------------
FEATURES
{
}

//-------------------------------------------------------------------------------------------------------------------------------------------------------------
COMMON
{
	#include "system.fxc" // This should always be the first include in COMMON
	#include "common.fxc"
	#include "math_general.fxc"
	#include "encoded_normals.fxc"

    #define PASS_INTERSECT 0
	#define PASS_REPROJECT 1
	#define PASS_PREFILTER 2
	#define PASS_RESOLVE_TEMPORAL 3
    

	DynamicCombo( D_PASS, 0..3, Sys( PC ) );

}

//-------------------------------------------------------------------------------------------------------------------------------------------------------------
CS
{
    #include "common/classes/Depth.hlsl"
    #include "common/classes/ScreenSpaceTrace.hlsl"
    #include "common/classes/Normals.hlsl"

	#define floatx float4
    
    Texture2D               PreviousFrameColor      < Attribute( "PreviousFrameColor" ); >;
	int 				    BlueNoiseIndex  		< Attribute( "BlueNoiseIndex" ); >;			// Blue noise texture

    Texture2D               Intersection            < Attribute( "Intersection" ); >;
	Texture2D 				DownsampledDepth		< Attribute( "DepthChainDownsample" ); >;
	Texture2D 				DownsampledDepthHistory	< Attribute( "DepthChainDownsamplePrevFrame" ); >;
	Texture2D 				ReflectionGBuffer		< Attribute( "ReflectionGBuffer" ); >;	// Reflection GBuffer, xy: encoded Normal, z: ray length in projection space, w: roughness
	Texture2D 				ReflectionGBufferHistory< Attribute( "ReflectionGBufferHistory" ); >;
	Texture2D 				ReprojectedRadiance		< Attribute( "ReprojectedRadiance" ); >;
	Texture2D 				Radiance				< Attribute( "Radiance" ); >;
	Texture2D 				RadianceHistory			< Attribute( "RadianceHistory" ); >;
    Texture2D 				AverageRadiance			< Attribute( "AverageRadiance" ); >;
	Texture2D 				AverageRadianceHistory	< Attribute( "AverageRadianceHistory" ); >;
	Texture2D 				Variance				< Attribute( "Variance" ); >;
	Texture2D 				VarianceHistory			< Attribute( "VarianceHistory" ); >;
	Texture2D 				SampleCount				< Attribute( "SampleCount" ); >;
	Texture2D 				SampleCountHistory		< Attribute( "SampleCountHistory" ); >;
	
	RWTexture2D<float4>		OutReprojectedRadiance	< Attribute( "OutReprojectedRadiance" ); >;
	RWTexture2D<float4>		OutAverageRadiance		< Attribute( "OutAverageRadiance" ); >;
	RWTexture2D<float>		OutVariance				< Attribute( "OutVariance" ); >;
	RWTexture2D<float4>		OutRadiance				< Attribute( "OutRadiance" ); >;
	RWTexture2D<float>		OutSampleCount			< Attribute( "OutSampleCount" ); >;

	float4 					Dimensions 	 	 		< Attribute("Dimensions"); >;	 		// Dimensions of the reflection buffer, xy: resolution, zw: 1 / resolution
    int                     SampleCountIntersection < Attribute("SampleCountIntersection"); Default(1); > ;
    int                     ReflectionDownsampleRatio < Attribute("ReflectionDownsampleRatio"); Default(0); > ; // Denominator of how much smaller the output buffer is than the hierarchical depth buffer, 0 means same size, 1 means half size, 2 means quarter size, etc.

    SamplerState            PointWrap < Filter( POINT ); >;
	SamplerState 			BilinearWrap < Filter( BILINEAR ); >;

	//--------------------------------------------------------------------------------------

    float LoadDepth(int2 pixel_coordinate, int mip = 0)
    {
        float flDepth = Tex2DLoad( DownsampledDepth, int3( pixel_coordinate, mip + ReflectionDownsampleRatio ) ).y;
        flDepth = RemapValClamped(flDepth, g_flViewportMinZ, g_flViewportMaxZ, 0.0f, 1.0f);

        return flDepth;
    }

    float3 ScreenSpaceToViewSpace(float3 screen_space_position) { return InvProjectPosition(screen_space_position, g_matProjectionToView); }

    // Todo: Use Motion:: class instead
    float2 GetReprojectedCoordinateFromLastFrame(uint2 pixel_coordinate)
    {
        float2 vUV = float2(pixel_coordinate) * Dimensions.zw;
        return ReprojectFromLastFrameSs(ScreenSpaceToWorldSpace(float3(vUV, LoadDepth(pixel_coordinate, 0))) + g_vCameraPositionWs.xyz).xy;
    }

    //--------------------------------------------------------------------------------------
    
    // GGX importance sampling function
    float3 ReferenceImportanceSampleGGX(float2 Xi, float roughness, float3 N)
    {
        float a = roughness * roughness;

        float phi = 2.0 * 3.141592 * Xi.x;
        float cosTheta = sqrt((1.0 - Xi.y) / (1.0 + (a * a - 1.0) * Xi.y));
        float sinTheta = sqrt(1.0 - cosTheta * cosTheta);

        float3 H;
        H.x = sinTheta * cos(phi);
        H.y = sinTheta * sin(phi);
        H.z = cosTheta;

        // Tangent space to world space
        float3 upVector = abs(N.z) < 0.999 ? float3(0, 0, 1) : float3(1, 0, 0);
        float3 T = normalize(cross(upVector, N));
        float3 B = cross(N, T);

        float3 sampleDirection = H.x * T + H.y * B + H.z * N;

        if ( any(isnan(sampleDirection) ) )
            return N;

        return normalize(sampleDirection);
    }

    void Pass_Reflections_Intersect( int2 vDispatch, int2 vGroupId )
    {
        const int nSamplesPerPixel = 1;

        //----------------------------------------------
        // Fetch stuff
        // ---------------------------------------------
        float3 vPositionWs = Depth::GetWorldPosition( vDispatch );
        const float3 vRayWs = normalize(  vPositionWs - g_vCameraPositionWs );

        //----------------------------------------------

        float3 vColor = 0;
        float flConfidence = 0;
        uint nValidSamples = 0;
        float flHitLength = 0;

        float InvSampleCount = 1.0 / nSamplesPerPixel;

        int nDownsampleRatio = 0;

        //----------------------------------------------
        [unroll]
        for ( uint k = 0; k < nSamplesPerPixel; k++ )
        {
            //----------------------------------------------
            // Get noise value
            // ---------------------------------------------
            float2 vDitherCoord = ( vDispatch + ( g_vRandomFloats * 256 ) ) % 256;
            float3 vNoise = Bindless::GetTexture2D(BlueNoiseIndex)[ vDitherCoord.xy ].rgb;

            // Randomize dir by roughness
            float flRoughness = Roughness::Sample( vDispatch );
            float3 vNormal = Normals::Sample( vDispatch );
            float3 H = ReferenceImportanceSampleGGX(vNoise.rg, flRoughness, vNormal);

            float3 vReflectWs = reflect(vRayWs, H);
            float2 vPositionSs = vDispatch;

            //----------------------------------------------
            // Trace reflection
            // ---------------------------------------------
            TraceResult trace = ScreenSpace::Trace( vPositionWs, vReflectWs, vPositionSs, 48, 0, nDownsampleRatio );

            //----------------------------------------------
            // Reproject
            // ---------------------------------------------
            float3 vHitWs = ScreenSpaceToWorldSpace(trace.HitClipSpace.xyz) + g_vCameraPositionWs.xyz;
            flHitLength = distance(vPositionWs, vHitWs); // Used for contact hardening

            int2 vLastFramePositionHitSs = ReprojectFromLastFrameSs(vHitWs).xy - 0.5f;
            vLastFramePositionHitSs = clamp(vLastFramePositionHitSs, 0, g_vViewportSize - 1); // Clamp to avoid out of bounds, allows us to reconstruct better

            //----------------------------------------------
            // Fetch and accumulate color and confidence
            // ---------------------------------------------
            bool bValidSample = (trace.Confidence > 0.0);

            vColor += PreviousFrameColor[ vLastFramePositionHitSs ].rgb;

            flConfidence += bValidSample * InvSampleCount;

            nValidSamples += bValidSample;
        }

        vColor *= InvSampleCount;

        OutRadiance[vDispatch] = float4(vColor * flConfidence, flConfidence);
    }

	//--------------------------------------------------------------------------------------
		
	float	FFX_DNSR_Reflections_GetRandom(int2 pixel_coordinate) 					{ return Bindless::GetTexture2D(BlueNoiseIndex)[ pixel_coordinate % 256 ].x; }

	float	FFX_DNSR_Reflections_LoadDepth(int2 pixel_coordinate) 					{ return LoadDepth( pixel_coordinate ); }
	float	FFX_DNSR_Reflections_LoadDepthHistory(int2 pixel_coordinate) 			{ return RemapValClamped( Tex2DLoad( DownsampledDepthHistory, int3( pixel_coordinate, ReflectionDownsampleRatio ) ).y, g_flViewportMinZ, g_flViewportMaxZ, 0.0f, 1.0f); } // Is this bullshit?
	float	FFX_DNSR_Reflections_SampleDepthHistory(float2 uv) 						{ return RemapValClamped( Tex2DLevelS( DownsampledDepthHistory, BilinearWrap, uv, ReflectionDownsampleRatio ).y, g_flViewportMinZ, g_flViewportMaxZ, 0.0f, 1.0f); } // Is this bullshit?

    float4	FFX_DNSR_Reflections_SampleAverageRadiance(float2 uv) 					{ return Tex2DLevelS( AverageRadiance, 		  BilinearWrap,	 uv, 0 ); }
	float4	FFX_DNSR_Reflections_SamplePreviousAverageRadiance(float2 uv) 			{ return Tex2DLevelS( AverageRadianceHistory, BilinearWrap, 	 uv, 0 ); }
	
	float4	FFX_DNSR_Reflections_LoadRadiance(int2 pixel_coordinate) 				{ return Tex2DLoad	( Radiance, 			int3( pixel_coordinate, 0 ) ); }
	float4	FFX_DNSR_Reflections_LoadRadianceHistory(int2 pixel_coordinate) 		{ return Tex2DLoad	( RadianceHistory, 		int3( pixel_coordinate, 0 ) ); }
	float4	FFX_DNSR_Reflections_LoadRadianceReprojected(int2 pixel_coordinate) 	{ return Tex2DLoad	( ReprojectedRadiance, 	int3( pixel_coordinate, 0 ) ); }
	float4	FFX_DNSR_Reflections_SampleRadianceHistory(float2 uv) 					{ return Tex2DLevelS( RadianceHistory, 		BilinearWrap, uv, 	0.0f ); }

	float	FFX_DNSR_Reflections_LoadNumSamples(int2 pixel_coordinate) 				{ return Tex2DLoad	( SampleCount, 			int3( pixel_coordinate, 0 ) ).x; }
	float	FFX_DNSR_Reflections_SampleNumSamplesHistory(float2 uv) 				{ return Tex2DLevelS( SampleCountHistory, 	BilinearWrap, uv, 	0.0f ).x; }

	float3	FFX_DNSR_Reflections_LoadWorldSpaceNormal(int2 pixel_coordinate) 		{ return Vector3VsToWs( normalize( float3( Tex2DLoad	( ReflectionGBuffer, 			int3( pixel_coordinate, 0 ) ).xy, 1.0f ) ) ); }
	float3	FFX_DNSR_Reflections_LoadWorldSpaceNormalHistory(int2 pixel_coordinate) { return Vector3VsToWs( normalize( float3( Tex2DLoad	( ReflectionGBufferHistory, 	int3( pixel_coordinate, 0 ) ).xy, 1.0f ) ) ); }
	float3	FFX_DNSR_Reflections_SampleWorldSpaceNormalHistory(float2 uv) 			{ return Vector3VsToWs( normalize( float3( Tex2DLevelS  ( ReflectionGBufferHistory, 	BilinearWrap, uv,    0   ).xy, 1.0f ) ) ); }
    
	float3	FFX_DNSR_Reflections_LoadViewSpaceNormal(int2 pixel_coordinate) 		{ return float3( Tex2DLoad	( ReflectionGBuffer, 			int3( pixel_coordinate, 0 ) ).xy, 1.0f ); }

	float	FFX_DNSR_Reflections_LoadRoughness(int2 pixel_coordinate) 				{ return Tex2DLoad	( ReflectionGBuffer, 		int3( pixel_coordinate, 0 ) ).w; }
	float	FFX_DNSR_Reflections_LoadRoughnessHistory(int2 pixel_coordinate) 		{ return Tex2DLoad	( ReflectionGBufferHistory, int3( pixel_coordinate, 0 ) ).w; }
	float	FFX_DNSR_Reflections_SampleRoughnessHistory(float2 uv) 					{ return Tex2DLevelS( ReflectionGBufferHistory, BilinearWrap, uv * Dimensions.xy, 0 ).w; }

    float2	FFX_DNSR_Reflections_LoadMotionVector(int2 pixel_coordinate) 			{ return ( pixel_coordinate - GetReprojectedCoordinateFromLastFrame(pixel_coordinate) )       * Dimensions.zw; } // No velocity buffer, sample the delta of the velocity from the last frame

    float	FFX_DNSR_Reflections_SampleVarianceHistory(float2 uv) 					{ return Tex2DLevelS( VarianceHistory, BilinearWrap, uv, 0 ).x; }
	float	FFX_DNSR_Reflections_LoadRayLength(int2 pixel_coordinate) 				{ return Tex2DLoad	( ReflectionGBuffer, int3( pixel_coordinate, 0 ) ).z ;}
	float	FFX_DNSR_Reflections_LoadVariance(int2 pixel_coordinate) 				{ return Tex2DLoad	( Variance, int3( pixel_coordinate, 0 ) ).x; }

    void	FFX_DNSR_Reflections_StoreRadianceReprojected(int2 pixel_coordinate, float3 value) 							{ OutReprojectedRadiance[pixel_coordinate.xy] 	= float4( value, 1.0f); }
	void	FFX_DNSR_Reflections_StoreAverageRadiance(int2 pixel_coordinate, float3 value) 								{ OutAverageRadiance[pixel_coordinate.xy] 		= float4( value, 1.0f ); }
	void	FFX_DNSR_Reflections_StoreVariance(int2 pixel_coordinate, float value) 										{ OutVariance[pixel_coordinate.xy] 				= value; }
	void	FFX_DNSR_Reflections_StoreNumSamples(int2 pixel_coordinate, float value) 									{ OutSampleCount[pixel_coordinate.xy] 			= value; }
	void	FFX_DNSR_Reflections_StoreTemporalAccumulation(int2 pixel_coordinate, float3 radiance, float variance) 		{ OutRadiance[pixel_coordinate] = min( float4( radiance.xyz, 1.0f ), 0 ); OutVariance[pixel_coordinate] = min( variance.x, 0 ); }
    void	FFX_DNSR_Reflections_StorePrefilteredReflections(int2 pixel_coordinate, float3 radiance, float variance)	{ OutRadiance[pixel_coordinate] = min( float4( radiance.xyz, 1.0f ), 0 ); OutVariance[pixel_coordinate] = min( variance.x, 0 ); }


    void	FFX_DNSR_Reflections_StoreRadianceReprojected(int2 pixel_coordinate, float4 value) 							{ OutReprojectedRadiance[pixel_coordinate.xy] 	= value; }
	void	FFX_DNSR_Reflections_StoreAverageRadiance(int2 pixel_coordinate, float4 value) 								{ OutAverageRadiance[pixel_coordinate.xy] 		= value; }
	void	FFX_DNSR_Reflections_StoreTemporalAccumulation(int2 pixel_coordinate, float4 radiance, float variance) 		{ OutRadiance[pixel_coordinate] = min( radiance, 0 ); OutVariance[pixel_coordinate] = min( variance.x, 0 ); }
    void	FFX_DNSR_Reflections_StorePrefilteredReflections(int2 pixel_coordinate, float4 radiance, float variance)	{ OutRadiance[pixel_coordinate] = min( radiance, 0 ); OutVariance[pixel_coordinate] = min( variance.x, 0 ); }

	bool 	FFX_DNSR_Reflections_IsGlossyReflection(float roughness) 						{ return roughness > 0.001; }
	bool 	FFX_DNSR_Reflections_IsMirrorReflection(float roughness) 						{ return !FFX_DNSR_Reflections_IsGlossyReflection(roughness); }
	float3 	FFX_DNSR_Reflections_ScreenSpaceToViewSpace(float3 screen_uv_coord) 			{ return ScreenSpaceToViewSpace(screen_uv_coord); } // UV and projection space depth
	float3 	FFX_DNSR_Reflections_ViewSpaceToWorldSpace(float4 view_space_coord) 			{ float4 vPositionPs = Position4VsToPs( view_space_coord ); return mul( vPositionPs, g_matProjectionToWorld ).xyz; }
	float3 	FFX_DNSR_Reflections_WorldSpaceToScreenSpacePrevious(float3 world_space_pos) 	{ return ReprojectFromLastFrameSs( world_space_pos); }
	float 	FFX_DNSR_Reflections_GetLinearDepth(float2 uv, float depth) 					{ float flDepth = LoadDepth(uv * Dimensions.xy); return ConvertDepthPsToVs( flDepth ); } // View space depth

	
    void FFX_DNSR_Reflections_LoadNeighborhood(
        int2 pixel_coordinate,
        out floatx radiance,
        out float variance,
        out float3 normal,
        out float depth,
        int2 screen_size)
    {
        radiance = FFX_DNSR_Reflections_LoadRadiance( pixel_coordinate );
        variance = FFX_DNSR_Reflections_LoadVariance( pixel_coordinate ).x;
        normal 	 = FFX_DNSR_Reflections_LoadWorldSpaceNormal( pixel_coordinate );
        depth 	 = FFX_DNSR_Reflections_LoadDepth( pixel_coordinate );
    }

	#define DISPATCH_OFFSET 4

	//--------------------------------------------------------------------------------------
	#if (D_PASS == PASS_REPROJECT)
		#include "common/thirdparty/ffx-reflection-dnsr/ffx_denoiser_reflections_reproject.h"
	#elif (D_PASS == PASS_PREFILTER)
		#include "common/thirdparty/ffx-reflection-dnsr/ffx_denoiser_reflections_prefilter.h"
	#elif (D_PASS == PASS_RESOLVE_TEMPORAL)
		#include "common/thirdparty/ffx-reflection-dnsr/ffx_denoiser_reflections_resolve_temporal.h"
	#endif
    //--------------------------------------------------------------------------------------

    [numthreads(8, 8, 1)]
    void MainCs(uint2 dispatchThreadID: SV_DispatchThreadID,
                uint2 groupThreadID: SV_GroupThreadID,
                uint localIndex: SV_GroupIndex,
                uint2 groupID: SV_GroupID)
    {
        uint2 group_thread_id 		= groupThreadID;
        uint2 dispatch_thread_id = dispatchThreadID;

        const float flReconstructMin = 0.1f;
        const float flReconstructMax = 0.9f;
        const float flMotionVectorScale = 1.0f;

        const float g_temporal_stability_factor = RemapValClamped( length( FFX_DNSR_Reflections_LoadMotionVector( dispatchThreadID ) * Dimensions.xy ) * flMotionVectorScale, 1.0, 0.0, flReconstructMin, flReconstructMax );

        #if ( D_PASS == PASS_INTERSECT )
        {
            //
            // Intersection Pass
            //
            Pass_Reflections_Intersect( dispatch_thread_id, group_thread_id );
        }
		#elif ( D_PASS == PASS_REPROJECT )
        {

            //
            // Reprojection Pass
            //
            FFX_DNSR_Reflections_Reproject( dispatch_thread_id, group_thread_id, Dimensions.xy, g_temporal_stability_factor, 32 );
        }
		#elif ( D_PASS == PASS_PREFILTER )
        {
			
            //
            // Prefilter
            //
            FFX_DNSR_Reflections_Prefilter( dispatch_thread_id, group_thread_id, Dimensions.xy );

            // Edge Gap Hardening
            int2 offsets[] = { int2(0,-3), int2(0,3), int2(-3,0), int2(3,0) };

            for( int i = 0; i < 4; i++ )
            {
                int2 offset = offsets[i];
                if ( any( ReflectionGBuffer[dispatch_thread_id].xy ) && !any( ReflectionGBuffer[dispatch_thread_id + offset].xy ) )
                {
                    OutRadiance[dispatch_thread_id] = OutRadiance[dispatch_thread_id - offset];
                    return;
                }
            }
		}
		#elif ( D_PASS == PASS_RESOLVE_TEMPORAL )
        {
			//
			// Temporal Resolve
			//
            FFX_DNSR_Reflections_ResolveTemporal(dispatch_thread_id, group_thread_id, Dimensions.xy, Dimensions.zw, g_temporal_stability_factor);
		}
		#endif

	}
}

