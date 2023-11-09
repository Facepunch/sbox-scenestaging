HEADER
{
	Description = "Sprite Shader for S&box";
}

MODES
{
	VrForward();
	Depth( S_MODE_DEPTH ); 
}

COMMON
{
	#include "code/shaders/system.hlsl"
}

struct VS_INPUT
{
	float3 pos : POSITION < Semantic( PosXyz ); >;
	float4 uv  : TEXCOORD0 < Semantic( LowPrecisionUv ); >;
    float4 normal : NORMAL < Semantic( None ); >;
    float4 tint : TEXCOORD1 < Semantic( None ); >;
	
    float4 sequence : TEXCOORD2 < Semantic( None ); >;

	uint instanceId : TEXCOORD13 < Semantic( InstanceTransformUv ); >; 
};

struct GS_INPUT
{
    float3 pos : POSITION;
    float4 normal : NORMAL;
    float4 uv : TEXCOORD0;
    float4 tint : COLOR0;
	
    float4 sequence : TEXCOORD1;

    uint instanceId : TEXCOORD13;
};

struct PS_INPUT
{
	float4 vPositionPs : SV_ScreenPosition;
	float4 uv : TEXCOORD0;
	float4 tint : TEXCOORD9;
};

VS
{
	GS_INPUT MainVs(const VS_INPUT i)
	{
	
		return i;
	
		//VertexInput o;

		//float3x4 mat = InstanceTransform( i.instanceId );
		//float3 ws = mul( mat, float4( i.pos, 1.0f ) );
		//o.vPositionPs.xyzw = Position3WsToPs( ws.xyz );
		//o.uv = i.uv;
		//o.tint = i.tint;

		//return o;
	}
}

GS
{
	float4 CalculateSpritePs(float3 vWorldSpace, float2 flPointSize, float2 vDelta, bool worldSize, float3 angles )
	{
		float4 resultPs;
		float3 offsets = 0;
	
		float3x3 mat = MatrixBuildRotationAboutAxis(g_vCameraDirWs, angles.z); // yaw
    

		if ( worldSize )
		{
			float3 vecCameraRightDir = cross( g_vCameraDirWs, g_vCameraUpDirWs );
			float3 offsets = 0;
			offsets += 0.5 * flPointSize.x * vDelta.x * vecCameraRightDir;
			offsets += 0.5 * flPointSize.y * vDelta.y * g_vCameraUpDirWs;
		
			offsets = mul(offsets, mat);
			vWorldSpace += offsets;

    }

		// transform into screenspace
		resultPs = Position3WsToPs(vWorldSpace);

		if ( !worldSize )
		{
			float2 vPixelSize = 1.0 * g_vInvViewportSize.xy;
			resultPs.xy += (flPointSize * vPixelSize.xy * vDelta.xy * resultPs.w);
		}

		return resultPs;
	}

	void CalculateSpriteVertex(out PS_INPUT o, VS_INPUT i, float2 vDelta)
	{
		float2 size = i.uv.xy;
	
		o.vPositionPs = CalculateSpritePs( i.pos.xyz, size, vDelta, true, i.normal.xyz );
		o.tint = i.tint.rgba;
		o.uv = 0;
		o.uv.xy = float2(vDelta.x * 0.5 + 0.5, 0.5 - vDelta.y * 0.5);
	}

	[maxvertexcount(4)]
	void MainGs(point GS_INPUT i[1], inout TriangleStream<PS_INPUT> triStream)
	{
		PS_INPUT o;

		CalculateSpriteVertex(o, i[0], float2(-1.0, 1.0));
		GSAppendVertex(triStream, o);
			
		CalculateSpriteVertex(o, i[0], float2(-1.0, -1.0));
		GSAppendVertex(triStream, o);

		CalculateSpriteVertex(o, i[0], float2(1.0, 1.0));
		GSAppendVertex(triStream, o);

		CalculateSpriteVertex(o, i[0], float2(1.0, -1.0));
		GSAppendVertex(triStream, o);

		GSRestartStrip(triStream);
		
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
		float2 uv = i.uv.xy;
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
