HEADER
{
	Description = "Template Shader for S&box";
}

MODES
{
	VrForward();
	Depth( S_MODE_DEPTH ); 
}

COMMON
{
	#include "system.hlsl"
}

struct VertexInput
{
	float3 pos : POSITION < Semantic( PosXyz ); >;
	float4 uv  : TEXCOORD0 < Semantic( LowPrecisionUv ); >;
	float4 tint : COLOR0 < Semantic( None ); >;

	uint instanceId : TEXCOORD13 < Semantic( InstanceTransformUv ); >; 
};

struct PS_INPUT
{
	float4 vPositionPs : SV_ScreenPosition;
	float4 uv : TEXCOORD0;
	float4 tint : TEXCOORD9;
};

VS
{
	PS_INPUT MainVs( const VertexInput i )
	{
		PS_INPUT o;

		float3x4 mat = InstanceTransform( i.instanceId );
		float3 ws = mul( mat, float4( i.pos, 1.0f ) );
		o.vPositionPs.xyzw = Position3WsToPs( ws.xyz );
		o.uv = i.uv;
		o.tint = i.tint;

		return o;
	}
}

PS
{
	#include "sheet_sampling.fxc"

	StaticCombo( S_MODE_DEPTH, 0..1, Sys( ALL ) );
	DynamicCombo( D_BLEND, 0..1, Sys( ALL ) );

	SamplerState g_sParticleTrilinearWrap < Filter( MIN_MAG_MIP_LINEAR ); MaxAniso( 1 ); >;

	CreateTexture2D( g_ColorTexture ) < Attribute( "BaseTexture" ); Filter( MIN_MAG_MIP_POINT );  SrgbRead( true ); >;
	float4 g_SheetData < Attribute( "BaseTextureSheet" ); >;

	#if ( D_BLEND == 0 )

		RenderState( BlendEnable, true );
		RenderState( SrcBlend, SRC_ALPHA );
		RenderState( DstBlend, INV_SRC_ALPHA );
		#if S_MODE_DEPTH == 0
		RenderState( DepthWriteEnable, false );
		#endif

	// additive
	#elif( D_BLEND == 1 ) 

		RenderState( BlendEnable, true );
		RenderState( SrcBlend, SRC_ALPHA );
		RenderState( DstBlend, ONE );
		RenderState( DepthWriteEnable, false );

	#endif

	float4 MainPs( PS_INPUT i ) : SV_Target0
	{
		float2 uv = i.uv;
		float4 col = 1;

		col = Tex2D( g_ColorTexture, uv);
		col.rgba *= i.tint;

		#if S_MODE_DEPTH
			OpaqueFadeDepth( pow( col.a, 0.3f ), i.vPositionPs.xy );
			return 1;
		#else
			clip (col.a - 0.01);
		#endif

		

		return col;
	}
}
