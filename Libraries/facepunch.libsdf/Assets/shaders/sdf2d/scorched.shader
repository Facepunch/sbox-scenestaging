//=========================================================================================================================
// Optional
//=========================================================================================================================
HEADER
{
	Description = "Simple shader for SDF 2D Layers with a scorch effect";
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
	#include "sdf2d/shared.hlsl"
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
};

//=========================================================================================================================

VS
{
	#include "common/vertex.hlsl"

	//
	// Main
	//
	PixelInput MainVs( INSTANCED_SHADER_PARAMS( VertexInput i ) )
	{
		PixelInput o = ProcessVertex( i );
		
		o.vPositionOs = i.vPositionOs;

		return FinalizeVertex( o );
	}
}

//=========================================================================================================================

PS
{
    #include "common/pixel.hlsl"

	CreateSdfLayerTexture( ScorchLayer );

	CreateInputTexture2D( ScorchColor, Srgb, 8, "", "_color",  "Scorch,10/10", Default3( 1.0, 1.0, 1.0 ) );
	CreateInputTexture2D( ScorchBlendMask, Linear, 8, "", "_height", "Scorch,10/20", Default( 1.0 ) );
	CreateInputTexture2D( ScorchRoughness, Linear, 8, "", "_rough", "Scorch,10/30", Default( 0.5 ) );
	CreateInputTexture2D( ScorchMetalness, Linear, 8, "", "_metal", "Scorch,10/40", Default( 1.0 ) );
	CreateInputTexture2D( ScorchAmbientOcclusion, Linear, 8, "", "_ao", "Scorch,10/50", Default( 1.0 ) );
	CreateInputTexture2D( ScorchNormal, Linear, 8, "NormalizeNormals", "_normal", "Scorch,10/60", Default3( 0.5, 0.5, 1.0 ) );
	
	CreateTexture2D( g_tScorchColor ) < Channel( RGB, Box( ScorchColor ), Srgb ); OutputFormat( BC7 ); SrgbRead( true ); > ;
	CreateTexture2D( g_tScorchNormal ) < Channel( RGB, Box( ScorchNormal ), Linear ); Channel( A, Box( ScorchBlendMask ), Linear ); OutputFormat( DXT5 ); SrgbRead( false );  >;
	CreateTexture2D( g_tScorchRma ) < Channel( R, Box( ScorchRoughness ), Linear ); Channel( G, Box( ScorchMetalness ), Linear ); Channel( B, Box( ScorchAmbientOcclusion ), Linear ); OutputFormat( BC7 ); SrgbRead( false ); >;

	//
	// Main
	//
	float4 MainPs( PixelInput i ) : SV_Target0
	{
		Material m = Material::From( i );
		
		float signedDist = SdfLayerTex( ScorchLayer, i.vPositionOs.xy ).r;
		float3 scorchColor = Tex2D( g_tScorchColor, i.vTextureCoords.xy ).rgb;
		float3 scorchRma = Tex2D( g_tScorchRma, i.vTextureCoords.xy ).xyz;
		float4 scorchMat = Tex2D( g_tScorchNormal, i.vTextureCoords.xy );
		float scorchMask = scorchMat.a;

		float scorch = 1.0 - clamp( (signedDist - scorchMask * 0.5) * 64.0, 0.0, 1.0 );
		float3 scorchNormal = TransformNormal( i, DecodeNormal( scorchMat.xyz ) );

		m.Albedo.rgb = lerp( m.Albedo, scorchColor, scorch );
		m.Normal.xyz = normalize( lerp( m.Normal.xyz, scorchNormal, scorch ) );
		m.Roughness = lerp( m.Roughness, scorchRma.x, scorch );
		m.Metalness = lerp( m.Metalness, scorchRma.y, scorch );
		m.AmbientOcclusion = lerp( m.AmbientOcclusion, scorchRma.z, scorch );

		return ShadingModelStandard::Shade( i, m );
	}
}
