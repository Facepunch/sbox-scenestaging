// Ideally you wouldn't need half these includes for an unlit shader
// But it's stupiod

FEATURES
{
    #include "common/features.hlsl"
}

MODES
{
    Forward();
    Depth();
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

	PixelInput MainVs( VertexInput i )
	{
		PixelInput o = ProcessVertex( i );
		// Add your vertex manipulation functions here
		return FinalizeVertex( o );
	}
}

PS
{
	StructuredBuffer<uint> GridBuffer < Attribute("GridBuffer"); >;

    #include "common/pixel.hlsl"
    #include "thirdparty/NanoVDB/VDBCommon.hlsli"
	//#include "thirdparty/NanoVDB/NanoVDB.hlsli"

    float4 MainPs( PixelInput i ) : SV_Target0
    {
        VdbSampler Sampler = InitVdbSampler(GridBuffer);

        // Global values (could be computed on CPU, and passed to shader instead)
        pnanovdb_vec3_t bbox_min = pnanovdb_coord_to_vec3(pnanovdb_root_get_bbox_min(Sampler.GridBuffer, Sampler.Root));
        pnanovdb_vec3_t bbox_max = pnanovdb_coord_to_vec3(pnanovdb_root_get_bbox_max(Sampler.GridBuffer, Sampler.Root));

        float3 WorldPosition = i.vPositionWithOffsetWs.xyz + g_vCameraPositionWs;
		float3 WorldPositionVDB = float3(bbox_max.x, bbox_max.y, bbox_max.z);

        pnanovdb_vec3_t position = { WorldPosition.x, WorldPosition.y, WorldPosition.z };
        float sample = TrilinearSampling(position, GridBuffer, Sampler.GridType, Sampler.Accessor);

        return float4(sample,0,0, 1 );
	}
}
