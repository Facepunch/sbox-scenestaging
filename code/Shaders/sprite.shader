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
    float4 velocity : TANGENT0 < Semantic( None ); >;
    float4 tint : TEXCOORD1 < Semantic( None ); >;
    float4 color : COLOR0 < Semantic( None ); >;

};

struct GS_INPUT
{
    float3 pos : POSITION;
    float4 uv : TEXCOORD0;
    float4 normal : NORMAL;
    float4 velocity : TANGENT0;
    float4 tint : COLOR0;
    float4 color : COLOR1 < Semantic( None ); >;

};


struct PS_INPUT
{
	float4 vPositionPs : SV_ScreenPosition;
	float4 uv : TEXCOORD0;
	float4 tint : TEXCOORD9;

  //  float4 sheet_uv0 : TEXCOORD20;
  //  float4 sheet_uv1 : TEXCOORD21;
   // float sheet_blend : TEXCOORD22;
};

VS
{
	GS_INPUT MainVs(const VS_INPUT i)
	{
		return i;
	}
}

GS
{

	float4 g_SheetData < Attribute( "BaseTextureSheet" ); >;

	bool g_ScreenSize < Attribute( "g_ScreenSize" ); >;

	float4 g_MotionBlur < Attribute( "g_MotionBlur" ); >;

	bool g_FaceVelocity < Attribute( "g_FaceVelocity" ); >;
	float g_FaceVelocityOffset < Attribute( "g_FaceVelocityOffset" ); >;

	float4 CalculateSpritePs(float3 vWorldSpace, float2 flPointSize, float2 vDelta, float3 angles )
	{
		float4 resultPs;
		float3 offsets = 0;
	
		float3 cameraAxis = g_vCameraDirWs;
	
		float3x3 mat = MatrixBuildRotationAboutAxis( cameraAxis, angles.z ); // yaw
    

		if ( !g_ScreenSize )
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

		if ( g_ScreenSize )
		{
			float2 vPixelSize = 1.0 * g_vInvViewportSize.xy;
			resultPs.xy += (flPointSize * vPixelSize.xy * vDelta.xy * resultPs.w);
		}

		return resultPs;
	}

	void CalculateSpriteVertex(out PS_INPUT o, in VS_INPUT i, in float2 vDelta)
	{
		float2 size = i.uv.xy;
	
		o.vPositionPs = CalculateSpritePs(i.pos.xyz, size, vDelta, i.normal.xyz);
		o.tint = i.tint.rgba;
		o.uv = 0;
		o.uv.xy = float2(vDelta.x * 0.5 + 0.5, 0.5 - vDelta.y * 0.5);
	
		float4 bounds = SampleSheet( g_SheetData, i.color.x, i.uv.z );
		o.uv.xy = bounds.xy + ( o.uv.xy * bounds.zw );
	}

	void DrawSprite( in GS_INPUT i, inout TriangleStream<PS_INPUT> triStream )
	{
		PS_INPUT o;

		CalculateSpriteVertex(o, i, float2(-1.0, 1.0));
		GSAppendVertex(triStream, o);
			
		CalculateSpriteVertex(o, i, float2(-1.0, -1.0));
		GSAppendVertex(triStream, o);

		CalculateSpriteVertex(o, i, float2(1.0, 1.0));
		GSAppendVertex(triStream, o);

		CalculateSpriteVertex(o, i, float2(1.0, -1.0));
		GSAppendVertex(triStream, o);

		GSRestartStrip(triStream);
	}



	[maxvertexcount(64)]
	void MainGs(point GS_INPUT i[1], inout TriangleStream<PS_INPUT> triStream)
	{
	
		if ( g_FaceVelocity )
		{	
			float4 ss = mul(g_matWorldToView, float4(i[0].velocity.xyz, 0));
			ss.z = 0;
			ss = normalize(ss);

        i[0].normal.b += ToDegrees * atan2(ss.y, ss.x) + g_FaceVelocityOffset;
    }
	
		DrawSprite( i[0], triStream );
	
		if ( g_MotionBlur.r == 0)
			return;
	
		i[0].tint.a *= g_MotionBlur.w;
	
		float3 velocity = i[0].velocity.xyz;
		float speed = length( velocity) * g_MotionBlur.y;
		float splots = speed / 50.0;
	
		if (splots < 1) return;
		if ( splots > 8 ) splots = 8;
	
		float3 scale = i[0].uv.x * (g_MotionBlur.z * 0.002f) * velocity;
	
		for (int f = 1; f < splots+1; f++ )
		{
			i[0].tint.a *= g_MotionBlur.w;
		
			// clamp alpha, because it's a waste of time drawing if we can't see it
			i[0].tint.a = clamp( i[0].tint.a, 0.001, 1 );
		
			GS_INPUT a = i[0];
			GS_INPUT b = a;
    
			// move along velocity vector
			a.pos.xyz += scale * f;
			b.pos.xyz -= scale * f;
					
			//
			// leading trail
			//
			if ( g_MotionBlur.r > 1 )
			{
				DrawSprite(a, triStream);
			}
		
			DrawSprite(b, triStream);
		}
		
	}
}

PS
{
	#include "sheet_sampling.fxc"

	StaticCombo( S_MODE_DEPTH, 0..1, Sys( ALL ) );
	DynamicCombo( D_BLEND, 0..1, Sys( ALL ) );

	SamplerState g_sParticleTrilinearWrap < Filter( MIN_MAG_MIP_LINEAR ); MaxAniso( 1 ); >;

	CreateTexture2D( g_ColorTexture ) < Attribute( "BaseTexture" ); Filter( BILINEAR ); AddressU( CLAMP ); AddressV( CLAMP ); AddressW( CLAMP ); SrgbRead( true ); >;
	float4 g_SheetData < Attribute( "BaseTextureSheet" ); >;

	// additive
	#if ( D_BLEND == 1 ) 
		RenderState( BlendEnable, true );
		RenderState( SrcBlend, SRC_ALPHA );
		RenderState( DstBlend, ONE );
		RenderState( DepthWriteEnable, false );
	#else 
		RenderState( BlendEnable, true );
		RenderState( SrcBlend, SRC_ALPHA );
		RenderState( DstBlend, INV_SRC_ALPHA );
		RenderState( BlendOp, ADD );
		RenderState( SrcBlendAlpha, ONE );
		RenderState( DstBlendAlpha, INV_SRC_ALPHA );
		RenderState( BlendOpAlpha, ADD );
	#endif

	#if S_MODE_DEPTH == 0
		RenderState( DepthWriteEnable, false );
	#endif

	float4 MainPs( PS_INPUT i ) : SV_Target0
	{
		float2 uv = i.uv.xy;
		float4 col = 1;
	
		col = Tex2D( g_ColorTexture, uv );
		col.rgba *= i.tint.rgba;
	
	    clip(col.a - 0.0001);

	
		//OpaqueFadeDepth(pow(col.a, 0.3f), i.vPositionPs.xy);
	
		#if S_MODE_DEPTH
			OpaqueFadeDepth( pow( col.a, 0.3f ), i.vPositionPs.xy );
			return 1;
		#elif (D_BLEND == 1)
			// transparency
		#else
			
			//OpaqueFadeDepth(pow(col.a, 0.1f), i.vPositionPs.xy);
			
			
		#endif
	

		return col;
	}
}
