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

	    // Opt out of stupid shit
    #define CUSTOM_MATERIAL_INPUTS
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

	// Ghost of Tsushima style grass blade deformation
	// Based on the original GDC presentation approach
	float3 GhostOfTsushimaBladeDeform(float3 basePos, Blade blade, float heightT)
	{
		// Base position starts at the blade root
		float3 pos = blade.Position;
		
		// Scale the entire base position by blade height first
		basePos *= blade.Height;

		// Apply width scaling to base position (for quad vertices)
		basePos.x *= blade.Width * 0.5; // Scale width
		
		// Create blade-local coordinate system
		float3 forward = normalize(float3(blade.Facing.x, blade.Facing.y, 0));
		float3 right = float3(-blade.Facing.y, blade.Facing.x, 0);
		float3 up = float3(0, 0, 1);
		
		// Apply facing rotation to base position
		float3 rotatedBase = basePos.x * right + basePos.y * forward + basePos.z * up;
		
		// Ghost of Tsushima curve calculation
		// Uses quadratic ease-out for natural blade curvature
		float curve = heightT * heightT * (3.0 - 2.0 * heightT); // Smooth step
		float heightScale = heightT * blade.Height;
		
		// Natural blade tilt (from wind and growth patterns)
		float tiltAmount = blade.Tilt * (3.14159 / 180.0); // Convert to radians
		float3 tiltDirection = forward * sin(tiltAmount) + up * (cos(tiltAmount) - 1.0);
		
		// Side curve for blade character variation
		float sideCurveAmount = blade.SideCurve * curve * curve; // More curve at the top
		float3 sideOffset = right * sideCurveAmount * blade.Height * 0.3;
		
		// Wind deformation with proper physics-based bending
		// Wind effect increases quadratically with height (like a cantilever beam)
		float windStrength = curve * curve;
		float3 windDeform = blade.Wind * windStrength * blade.Bend;
		
		// Clump coherence - blades in the same clump bend similarly
		float clumpInfluence = 0.6; // How much clumps affect individual blades
		float2 clumpWindDir = normalize(blade.ClumpFacing);
		float clumpWindAmount = dot(normalize(blade.Wind.xy), clumpWindDir);
		float3 clumpDeform = float3(clumpWindDir * clumpWindAmount * windStrength * 0.5, 0);
		windDeform = lerp(windDeform, windDeform + clumpDeform, clumpInfluence);
		
		// Combine all deformations
		float3 finalPos = pos + rotatedBase;
		finalPos.z += heightScale; // Apply height
		finalPos += tiltDirection * heightScale * 0.4; // Apply tilt
		finalPos += sideOffset; // Apply side curve
		finalPos += windDeform; // Apply wind
		
		// Prevent grass from going underground due to extreme bending
		finalPos.z = max(finalPos.z, pos.z);
		
		return finalPos;
	}

	// Calculate normal based on Ghost of Tsushima's approach
	float3 CalculateGrassNormal(Blade blade, float heightT, float3 worldPos)
	{
		// Calculate tangent by sampling nearby positions on the curve
		float epsilon = 0.02;
		float3 pos1 = GhostOfTsushimaBladeDeform(float3(0, 0, 0), blade, max(0, heightT - epsilon));
		float3 pos2 = GhostOfTsushimaBladeDeform(float3(0, 0, 0), blade, min(1, heightT + epsilon));
		
		float3 tangent = normalize(pos2 - pos1);
		
		// Create blade-local coordinate system
		float3 forward = normalize(float3(blade.Facing.x, blade.Facing.y, 0));
		float3 right = float3(-blade.Facing.y, blade.Facing.x, 0);
		
		// Base normal is perpendicular to the blade (facing the camera)
		float3 baseNormal = right;
		
		// Adjust normal based on blade curvature
		// When blade bends, normal should follow the surface orientation
		float bendFactor = 1.0 - abs(dot(tangent, float3(0, 0, 1)));
		float3 bentNormal = normalize(lerp(baseNormal, tangent, bendFactor * 0.3));
		
		return bentNormal;
	}

	PixelInput MainVs( VertexInput i )
	{
		PixelInput o = (PixelInput)0;
		
		// Get blade data
		Blade blade = GrassBladeBuffer[i.DrawID];
		
		// Height parameter represents position along the blade (0 = root, 1 = tip)
		float heightT = i.Height;
		
		// Apply Ghost of Tsushima blade deformation
		o.WorldPos = GhostOfTsushimaBladeDeform(i.Position, blade, heightT);
		o.Position = Position3WsToPs(o.WorldPos);
		
		// Calculate proper normal for lighting
		o.Normal.xyz = CalculateGrassNormal(blade, heightT, o.WorldPos);
		
		// Ambient occlusion based on height and blade density
		// Lower parts of grass get more AO, thinner blades get less AO
		float baseAO = 1.0 - heightT; // More AO at base
		float widthAO = saturate(blade.Width * 0.5); // Wider blades cast more shadow
		o.Normal.w = baseAO * widthAO;
		
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

		Material m = Material::Init( i );

		m.WorldPosition = i.WorldPos;
		m.WorldPositionWithOffset = i.WorldPos + g_vCameraPositionWs;
		m.ScreenPosition = i.Position;
		m.Normal = i.Normal.xyz;
		m.AmbientOcclusion = 1.0 - (i.Normal.w);

		m.Albedo = blade.Color;

		return ShadingModelStandard::Shade( m );
	}
}
