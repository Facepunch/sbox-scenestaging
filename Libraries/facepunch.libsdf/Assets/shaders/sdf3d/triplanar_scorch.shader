//=========================================================================================================================
// Optional
//=========================================================================================================================
HEADER
{
	CompileTargets = ( IS_SM_50 && ( PC || VULKAN ) );
	Description = "Shader for scorchable geometry without texture coordinates";
}

//=========================================================================================================================
// Optional
//=========================================================================================================================
FEATURES
{
    #include "common/features.hlsl"
}

//=========================================================================================================================
COMMON
{
	#include "common/shared.hlsl"
}

//=========================================================================================================================

struct VertexInput
{
	#include "common/vertexinput.hlsl"
};

//=========================================================================================================================

struct PixelInput
{
	#include "common/pixelinput.hlsl"

	float3 vPositionOs : TEXCOORD15;
	float3 vNormalOs : TEXCOORD16;
};

//=========================================================================================================================

VS
{
	#include "common/vertex.hlsl"

	//
	// Main
	//
	PixelInput MainVs( VertexInput i )
	{
		PixelInput o = ProcessVertex( i );

		o.vPositionOs = i.vPositionOs;
		o.vNormalOs = i.vNormalOs.xyz;

		return FinalizeVertex( o );
	}
}

//=========================================================================================================================

PS
{
	#define TRIPLANAR_OBJECT_SPACE
	
	#include "sdf3d/triplanar.hlsl"
	#include "sdf3d/shared.hlsl"

	CreateSdfLayerTexture( ScorchLayer );

	CreateInputTexture2D( ScorchColor, Srgb, 8, "", "_color",  "Scorch,10/10", Default3( 1.0, 1.0, 1.0 ) );
	CreateInputTexture2D( ScorchBlendMask, Linear, 8, "", "_height", "Scorch,10/20", Default( 0.5 ) );
	CreateInputTexture2D( ScorchRoughness, Linear, 8, "", "_rough", "Scorch,10/30", Default( 0.5 ) );
	CreateInputTexture2D( ScorchMetalness, Linear, 8, "", "_metal", "Scorch,10/40", Default( 1.0 ) );
	CreateInputTexture2D( ScorchAmbientOcclusion, Linear, 8, "", "_ao", "Scorch,10/50", Default( 1.0 ) );
	CreateInputTexture2D( ScorchNormal, Linear, 8, "NormalizeNormals", "_normal", "Scorch,10/60", Default3( 0.5, 0.5, 1.0 ) );
	
	CreateTexture2D( g_tScorchColor ) < Channel( RGB, Box( ScorchColor ), Srgb ); Channel( A, Box( ScorchBlendMask ), Linear ); OutputFormat( BC7 ); SrgbRead( true ); > ;
	CreateTexture2D( g_tScorchNormal ) < Channel( RGB, Box( ScorchNormal ), Linear ); OutputFormat( DXT5 ); SrgbRead( false );  >;
	CreateTexture2D( g_tScorchRma ) < Channel( R, Box( ScorchRoughness ), Linear ); Channel( G, Box( ScorchMetalness ), Linear ); Channel( B, Box( ScorchAmbientOcclusion ), Linear ); OutputFormat( BC7 ); SrgbRead( false ); >;

	//
	// Main
	//
	float4 MainPs( PixelInput i ) : SV_Target0
	{
		Material m = ToMaterialTriplanar( i, g_tColor, g_tNormal, g_tRma );
		Material s = ToMaterialTriplanar( i, g_tScorchColor, g_tScorchNormal, g_tScorchRma );

		float scorchMask = s.Opacity;

		float signedDist = SdfLayerTex( ScorchLayer, i.vPositionOs.xyz ).r;
		float scorch = 1.0 - clamp( signedDist * 4.0 - scorchMask * 64.0, 0.0, 1.0 );

		m.Opacity = 1;
		m.Albedo = lerp( m.Albedo, s.Albedo, scorch );
		m.Normal.xyz = normalize( lerp( m.Normal.xyz, s.Normal.xyz, scorch ) );
		m.Roughness = lerp( m.Roughness, s.Roughness, scorch );
		m.Metalness = lerp( m.Metalness, s.Metalness, scorch );
		m.AmbientOcclusion = lerp( m.AmbientOcclusion, s.AmbientOcclusion, scorch );

		return ShadingModelStandard::Shade( i, m );
	}
}
