FEATURES
{
    #include "common/features.hlsl"
}

MODES
{
    Forward();
    Depth( S_MODE_DEPTH );
}

COMMON
{
	#include "common/shared.hlsl"
	#include "GrassCommon.hlsl"
}

struct VertexInput
{
	float3 Position : POSITION < Semantic( PosXyz ); >;
	float Height : TEXCOORD0 < Semantic( LowPrecisionUv ); >;
	uint DrawID : SV_InstanceID < Semantic( InstanceTransformUv ); >;
	float4 ScreenPosition : SV_Position < Semantic( PosXyz ); >;
};

struct PixelInput
{
	float4 Position : SV_Position;
	float3 WorldPos : TEXCOORD0;
	float4 Normal : TEXCOORD1;
	uint DrawID : TEXCOORD2;
};



VS
{
	StructuredBuffer<uint> BladeCounter < Attribute( "BladeCounter" ); >;

	// Add bezier curve logic to the vertex shader
	float3 BezierCurve(float3 p0, float3 p1, float3 p2, float t) {
		float u = 1.0 - t;
		return u * u * p0 + 2.0 * u * t * p1 + t * t * p2;
	}

	PixelInput MainVs( VertexInput i )
	{
		// Null initialize pixelinput
		PixelInput o = (PixelInput)0;

		i.DrawID = i.DrawID;

		// Retrieve blade data
		Blade blade = GrassBladeBuffer[i.DrawID];

		// Calculate scale
		i.Position *= blade.Height;

		// Calculate bezier curve for blade bending
		float3 p1 = blade.Position;
		float3 p2 = blade.Position + ( blade.Wind * 50 ) * blade.Height;
		p2.z -= length( blade.Wind ) * 25.0f; // Bend it down based on wind length
		float3 bentPosition = BezierCurve(blade.Position, p1, p2, i.Height);

		// Apply rotation and position
		float2 facing = blade.Facing;
		float3 rotatedPos = float3(
			i.Position.x * facing.x - i.Position.y * facing.y,
			i.Position.x * facing.y + i.Position.y * facing.x,
			i.Position.z
		);

		o.WorldPos = rotatedPos + bentPosition;
		o.Position = Position3WsToPs(o.WorldPos);
		o.Normal.xyz = float3(facing.x, 0, facing.y);

		// Ambient occlusion, smaller ones have less AO
		o.Normal.w = i.Height * ( saturate( blade.Height) );
		
		// TODO: Calculate the normal based on the bending as well

		o.DrawID = i.DrawID;

		return o;
	}
}

PS
{
    #include "common/pixel.hlsl"
	
	RenderState( CullMode, NONE );

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		Blade blade = GrassBladeBuffer[i.DrawID];

		Material m = Material::Init();

		m.WorldPosition = i.WorldPos;
		m.WorldPositionWithOffset = i.WorldPos + g_vCameraPositionWs;
		m.ScreenPosition = i.Position;
		m.Normal = i.Normal.xyz;
		m.AmbientOcclusion = i.Normal.w * i.Normal.w;

		m.Albedo = blade.Color;

		//m.Roughness = 0.2f;

		return ShadingModelStandard::Shade( m );
	}
}
