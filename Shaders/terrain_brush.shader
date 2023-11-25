HEADER
{
	Description = "Terrain Brush Preview";
    DevShader = true;
    DebugInfo = false;
}

FEATURES
{
    #include "vr_common_features.fxc"
}

MODES
{
    VrForward();

    ToolsVis( S_MODE_TOOLS_VIS );
}

COMMON
{
    #include "system.fxc"
    #include "vr_common.fxc"


}

struct VertexInput
{
	float3 Position     : POSITION		< Semantic( PosXyz ); >;
	float3 DecalOrigin  : TEXCOORD0		< Semantic( Uvwx ); >;
	uint nInstanceTransformID				: TEXCOORD13	< Semantic( InstanceTransformUv ); >;
};

struct PixelInput
{
	float3 WorldPosition : TEXCOORD0;
	float3 DecalRight	: TEXCOORD1;
	float3 DecalForward	: TEXCOORD2;
	float3 DecalUp		: TEXCOORD3;
	float3 CameraToPositionRay          : TEXCOORD4;
	float3 DecalOrigin		: TEXCOORD5;



    #if ( PROGRAM == VFX_PROGRAM_VS )
        float4 PixelPosition : SV_Position;
    #endif

    #if ( PROGRAM == VFX_PROGRAM_PS )
        float4 ScreenPosition : SV_ScreenPosition;
    #endif
};

VS
{
	// absolute fucking idiot
	#define VS_INPUT VertexInput

	#include "instancing.fxc"

	DynamicComboRule( Allow0( D_SKINNING ) )

	PixelInput MainVs( VertexInput i )
	{
        PixelInput o;

		float3x4 matObjectToWorld = CalculateInstancingObjectToWorldMatrix( i );
		float3 vVertexPosWs = mul( matObjectToWorld, float4( i.Position.xyz, 1.0 ) );
		o.PixelPosition.xyzw = Position3WsToPs( vVertexPosWs.xyz );

		o.WorldPosition = vVertexPosWs;
		o.DecalOrigin = mul( matObjectToWorld, float4( 0, 0, 0, 1.0 ) );
		o.DecalRight = float3( 0, -1, 0 );
		o.DecalForward = float3( 1.0, 0, 0 );
		o.DecalUp = float3( 0, 0, 1.0 );

        o.CameraToPositionRay.xyz = CalculateCameraToPositionRayWs( vVertexPosWs );

		return o;
	}
}

//=========================================================================================================================

PS
{
	DynamicCombo( D_MSAA_DEPTH_BUFFER, 0..1, Sys( ALL ) );

	// fucking christ
	#if ( D_MSAA_DEPTH_BUFFER )
		CreateTexture2DMS( g_tSceneDepth ) < Attribute( "SceneDepth" ); SrgbRead( false ); Filter( MIN_MAG_MIP_POINT ); AddressU( CLAMP ); AddressV( CLAMP ); >;
	#else
		CreateTexture2D( g_tSceneDepth ) < Attribute( "SceneDepth" ); SrgbRead( false ); Filter( MIN_MAG_MIP_POINT ); AddressU( CLAMP ); AddressV( CLAMP ); >;
	#endif

	CreateTexture2D( g_tBrush ) < Attribute( "Brush" ); SrgbRead( false ); Filter( MIN_MAG_MIP_POINT ); AddressU( WRAP ); AddressV( WRAP ); >;

	float g_flRadius < Attribute( "Radius" ); Default( 16.0f ); >;

	#define COLOR_WRITE_ALREADY_SET
	RenderState( ColorWriteEnable0, RGB );

    #define BLEND_MODE_ALREADY_SET
    RenderState( BlendEnable, true );
    RenderState( BlendOp, ADD );
    RenderState( SrcBlend, SRC_ALPHA );
    RenderState( DstBlend, INV_SRC_ALPHA );

    #define DEPTH_STATE_ALREADY_SET
	RenderState( CullMode, FRONT );
	RenderState( DepthEnable, false );
	RenderState( DepthWriteEnable, false );
	RenderState( DepthFunc, GREATER_EQUAL );

    #include "vr_common_ps_code.fxc"

	float3 CalculateWorldSpacePosition( float3 vCameraToPositionRayWs, float2 vPositionSs, int2 vOffset, int nMsaaSample )
	{
		// Calculate view ray direction
		float3 vCameraToPositionDirWs = vCameraToPositionRayWs.xyz;
		vCameraToPositionDirWs.xyz += vOffset.x * ddx( vCameraToPositionRayWs.xyz );
		vCameraToPositionDirWs.xyz += vOffset.y * ddy( vCameraToPositionRayWs.xyz );
		vCameraToPositionDirWs.xyz = normalize( vCameraToPositionDirWs.xyz );

		// Calculate depth
		float flProjectedDepth;
		#if ( D_MSAA_DEPTH_BUFFER )
		{
			flProjectedDepth = Tex2DMS( g_tSceneDepth, vPositionSs.xy + vOffset.xy, nMsaaSample ).x;
		}
		#else
		{
			flProjectedDepth = Tex2DLoad( g_tSceneDepth, int3( vPositionSs.xy + vOffset.xy, 0 ) ).x;
		}
		#endif

		// Remap depth to viewport depth range
		flProjectedDepth = RemapValClamped( flProjectedDepth, g_flViewportMinZ, g_flViewportMaxZ, 0.0, 1.0 );

		// Recover
		float3 vPositionWs = RecoverWorldPosFromProjectedDepthAndRay( flProjectedDepth, vCameraToPositionDirWs.xyz );

		return vPositionWs.xyz;
	}
	
	//-----------------------------------------------------------------------------------------------------------------
	float3 CaclulateDecalSpacePosition( float3 vPositionWs, float3 vDecalOrigin, float3 DecalRight, float3 DecalUp, float3 DecalForward )
	{
		float3 vDelta = vPositionWs.xyz - vDecalOrigin.xyz;

		float3 vPositionDs;
		vPositionDs.x = dot( vDelta, DecalRight );
		vPositionDs.y = 1.0 - dot( vDelta, DecalUp );
		vPositionDs.z = dot( vDelta, DecalForward );
		return vPositionDs.xyz;
	}

	float InsideRegion( float3 vPositionDs )
	{
		float3 vInside =
			step( float3( 0.0, 0.0, 0.0 ), vPositionDs.xyz ) *
			step( vPositionDs.xyz, float3( 1.0, 1.0, 1.0 ) );
		return ( vInside.x * vInside.y * vInside.z );
	}

	bool InDecalBounds( float3 worldPos, float3 decalPos, float3 DecalRight, float3 DecalUp, float3 DecalForward )
	{
		float3 scale = g_flRadius;
		float3 p = worldPos - decalPos;
		return abs(dot(p, DecalRight)) <= scale.x && abs(dot(p, DecalForward)) <= scale.y && abs(dot(p, DecalUp)) <= scale.z;
	}

	float4 MainPs( PixelInput i ) : SV_Target0
	{
        float3 vPositionWs = CalculateWorldSpacePosition( i.CameraToPositionRay, i.ScreenPosition.xy, int2( 0, 0 ), 0 );
        // float3 vPositionDs = CaclulateDecalSpacePosition( vPositionWs, i.WorldDecalOrigin, float3( 0, -1, 0 ) * 8, float3( 1, 0, 0 ) * 8, float3( 0, 0, 1 ) * 8 );
        
		float3 vPositionDs = CaclulateDecalSpacePosition( vPositionWs, i.DecalOrigin, i.DecalRight, i.DecalUp, i.DecalForward );

		if ( InDecalBounds( vPositionWs, i.DecalOrigin, i.DecalRight, i.DecalUp, i.DecalForward ) )
		{
			// g_tBrush

			// vec4 local_position = (vec4(world_position - decal_position, 0.0)) * WORLD_MATRIX;
			// vec2 uv_coords = (vec2(local_position.x, -local_position.y)  / (4.0*(decal_half_scale.xz * 2.0 * decal_half_scale.xz))) - vec2(0.5);

			float3 localPos = i.DecalOrigin - vPositionWs;
			float2 uv = float2( localPos.x, -localPos.y ) / ( 2 * g_flRadius ) - float2( 0.5, 0.5 );

			return float4( 0.2, 0.2, 0.8, Tex2D( g_tBrush, uv /*localPos.xy / 600 + float2( 0.5, 0.5 )*/ ).r );
		}

		clip( -1 );



        // clip( InsideRegion( vPositionDs ) - 0.5 );
		// return float4 ( 0, 0, 0, 1 );

		// return float4( 0, 0, length(vPositionDs) / 1000, 1 );
		return float4( 0, 0, vPositionWs.z / 1000, 1 );
	}
}