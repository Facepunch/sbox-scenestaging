HEADER
{
	Description = "Standard shader but it bends, proof of concept, will be made into a proper feature with compute shaders";
}

MODES
{
	VrForward();
	Depth();
	ToolsVis( S_MODE_TOOLS_VIS );
}

FEATURES
{
    #include "common/features.hlsl"
}

COMMON
{
	#include "common/shared.hlsl"
}

struct VertexInput
{
	#include "common/vertexinput.hlsl"
};

struct PixelInput
{
	#include "common/pixelinput.hlsl"
};

VS
{
	#include "common/vertex.hlsl"

	StructuredBuffer<float4> Lattice < Attribute("Lattice"); >;
	float4x4 LocalToLattice < Attribute( "LocalToLattice" ); >;
	float3 Scale < Attribute( "Scale" ); >;
	float3 Segments < Attribute( "Segments"); >;

	int Convert3DTo1D(int x, int y, int z)
	{
		return x * (Segments.y * Segments.z) + y * Segments.z + z;
	}

	PixelInput MainVs( VertexInput i )
	{
		float3 PositionToLattice = mul( LocalToLattice, float4( i.vPositionOs, 1 ) ).xyz;
		float3 vPositionOs = i.vPositionOs;
		// Only influence if bbox is inside the vertex
		if (all(PositionToLattice >= 0.0f && PositionToLattice <= 1.0f))
        {
			// Get all 8 neighbors
			float3 frac = PositionToLattice * Segments;
			int3 p = floor(frac);

			float3 neighbors[8];
			neighbors[0] = Lattice[Convert3DTo1D(p.x, p.y, p.z)].xyz;
			neighbors[1] = Lattice[Convert3DTo1D(p.x + 1, p.y, p.z)].xyz;
			neighbors[2] = Lattice[Convert3DTo1D(p.x, p.y + 1, p.z)].xyz;
			neighbors[3] = Lattice[Convert3DTo1D(p.x + 1, p.y + 1, p.z)].xyz;
			neighbors[4] = Lattice[Convert3DTo1D(p.x, p.y, p.z + 1)].xyz;
			neighbors[5] = Lattice[Convert3DTo1D(p.x + 1, p.y, p.z + 1)].xyz;
			neighbors[6] = Lattice[Convert3DTo1D(p.x, p.y + 1, p.z + 1)].xyz;
			neighbors[7] = Lattice[Convert3DTo1D(p.x + 1, p.y + 1, p.z + 1)].xyz;

			frac = frac - p;

			// Lerp each fraction of the 8 neighbors in the right direction
			// Doesn't look fully correct
			float3 offset = lerp( lerp( lerp( neighbors[0], neighbors[1], frac.x ), lerp( neighbors[2], neighbors[3], frac.x ), frac.y ), lerp( lerp( neighbors[4], neighbors[5], frac.x ), lerp( neighbors[6], neighbors[7], frac.x ), frac.y ), frac.z );
			
			i.vPositionOs += offset * Scale;

			// Todo: warp normals too
        }


		PixelInput o = ProcessVertex( i );		
		return FinalizeVertex( o );
	}
}

//=========================================================================================================================

PS
{
    #include "common/pixel.hlsl"
	

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		Material m = Material::From( i );
		m.Metalness = 1.0f;
		m.Roughness = 0.1f;
		return ShadingModelStandard::Shade( i, m );
	}
}
