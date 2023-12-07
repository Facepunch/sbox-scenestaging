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
	#include "common/shared.hlsl"
}

struct VertexInput
{
	float3 Position     		: POSITION		< Semantic( PosXyz ); >;
	float3 DecalOrigin  		: TEXCOORD0		< Semantic( Uvwx ); >;
	uint nInstanceTransformID	: TEXCOORD13	< Semantic( InstanceTransformUv ); >;
};

struct PixelInput
{
	float3 WorldPosition		: TEXCOORD0;
	float3 DecalRight			: TEXCOORD1;
	float3 DecalForward			: TEXCOORD2;
	float3 DecalUp				: TEXCOORD3;
	float3 CameraToPositionRay	: TEXCOORD4;
	float3 DecalOrigin			: TEXCOORD5;

    #if ( PROGRAM == VFX_PROGRAM_VS )
        float4 PixelPosition : SV_Position;
    #endif

    #if ( PROGRAM == VFX_PROGRAM_PS )
        float4 ScreenPosition : SV_ScreenPosition;
    #endif
};

VS
{
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
	CreateTexture2D( g_tBrush ) < Attribute( "Brush" ); SrgbRead( false ); Filter( BILINEAR ); AddressU( WRAP ); AddressV( WRAP ); >;

	float g_flRadius < Attribute( "Radius" ); Default( 16.0f ); >;
	float4 g_flColor < Attribute( "Color" ); >;

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

	// Could use Depth::GetWorldPosition in the future if it supports giving rays?
	float3 CalculateWorldSpacePosition( float3 vCameraToPositionRayWs, float2 vPositionSs )
	{
		float depth = Depth::GetNormalized( vPositionSs.xy );
		return RecoverWorldPosFromProjectedDepthAndRay( depth, vCameraToPositionRayWs );
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
        float3 vPositionWs = CalculateWorldSpacePosition( i.CameraToPositionRay, i.ScreenPosition.xy );
		float3 vPositionDs = CaclulateDecalSpacePosition( vPositionWs, i.DecalOrigin, i.DecalRight, i.DecalUp, i.DecalForward );

		if ( InDecalBounds( vPositionWs, i.DecalOrigin, i.DecalRight, i.DecalUp, i.DecalForward ) )
		{
			float3 localPos = i.DecalOrigin - vPositionWs;
			float2 uv = float2( localPos.x, -localPos.y ) / ( 2 * g_flRadius ) - float2( 0.5, 0.5 );

			float opacity = Tex2D( g_tBrush, uv ).r;
			float4 color = float4( g_flColor.rgb, g_flColor.a * opacity );

			return color;
		}

		clip( -1 );

		return float4( 0, 0, vPositionWs.z / 1000, 1 );
	}
}