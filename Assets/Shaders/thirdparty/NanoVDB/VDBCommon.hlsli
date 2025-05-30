// Copyright 2022 Eidos-Montreal / Eidos-Sherbrooke

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http ://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

#ifndef D_VDB_VOLUMETRIC_COMMON
#define D_VDB_VOLUMETRIC_COMMON

// http://graphics.cs.cmu.edu/courses/15-468/lectures/lecture12.pdf
// https://media.contentapi.ea.com/content/dam/eacom/frostbite/files/s2016-pbs-frostbite-sky-clouds-new.pdf
#define ANALYTIC_INTEGRATION 

#include "thirdparty/NanoVDB/NanoVDB.hlsli"

//-----------------------------------------------------------------------------------------------------------
// Random Sequence
//-----------------------------------------------------------------------------------------------------------
struct RandomSequence
{
    // Seed value (should be initialized externally per pixel or sample)
    uint state;
};

uint LCG(inout uint state)
{
    // Linear Congruential Generator constants
    state = 1664525u * state + 1013904223u;
    return state;
}

float RandomSequence_GenerateSample1D(inout RandomSequence seq)
{
    // Generate a random uint and convert to float in [0,1)
    uint rnd = LCG(seq.state);
    // Mask to improve randomness in lower bits and divide to normalize
    return (float)(rnd & 0x00FFFFFFu) / 16777216.0f;
}

float2 RandomSequence_GenerateSample2D(inout RandomSequence seq)
{
    // Generate two independent 1D samples for a 2D sample
    return float2(
        RandomSequence_GenerateSample1D(seq),
        RandomSequence_GenerateSample1D(seq)
    );
}

//-----------------------------------------------------------------------------------------------------------
#define POSITIVE_INFINITY 1.0e38
#define PI 3.1415926535897932384626433832795

struct Segment
{
	pnanovdb_vec3_t Start;
	pnanovdb_vec3_t End;
};

struct VdbRay
{
	pnanovdb_vec3_t Origin;
	pnanovdb_vec3_t Direction;
	float TMin;
	float TMax;
};

struct HeterogenousMedium
{
	float densityScale;
	float densityMin;
	float densityMax;
	float anisotropy;
	float albedo;
};

struct VdbSampler
{
	pnanovdb_grid_handle_t Grid;
	pnanovdb_buf_t GridBuffer;
	pnanovdb_readaccessor_t Accessor;
	pnanovdb_uint32_t GridType;
	pnanovdb_root_handle_t Root;
};

VdbSampler InitVdbSampler(pnanovdb_buf_t buf)
{
	VdbSampler Sampler;
	Sampler.GridBuffer = buf;

	pnanovdb_address_t address; address.byte_offset = 0;
	Sampler.Grid.address = address;

	pnanovdb_buf_t root_buf = buf;
	pnanovdb_tree_handle_t tree = pnanovdb_grid_get_tree(Sampler.GridBuffer, Sampler.Grid);
	Sampler.Root = pnanovdb_tree_get_root(root_buf, tree);

	pnanovdb_readaccessor_init(Sampler.Accessor, Sampler.Root);

	Sampler.GridType = pnanovdb_grid_get_grid_type(Sampler.GridBuffer, Sampler.Grid);

	return Sampler;
}

//-----------------------------------------------------------------------------------------------------------
// NanoVDB Buffers
//-----------------------------------------------------------------------------------------------------------

float ReadValue(pnanovdb_coord_t ijk, pnanovdb_buf_t buf, pnanovdb_uint32_t grid_type, in out pnanovdb_readaccessor_t acc)
{
	pnanovdb_uint32_t level;
	pnanovdb_address_t address = pnanovdb_readaccessor_get_value_address_and_level(grid_type, buf, acc, ijk, level);
	return pnanovdb_root_read_float_typed(grid_type, buf, address, ijk, level);
}

float ReadValue(pnanovdb_vec3_t pos, pnanovdb_buf_t buf, pnanovdb_uint32_t grid_type, pnanovdb_readaccessor_t acc)
{
	pnanovdb_coord_t ijk = pnanovdb_hdda_pos_to_ijk(pos);
	return ReadValue(ijk, buf, grid_type, acc);
}

float3 ReadValueVec3f(pnanovdb_coord_t ijk, pnanovdb_buf_t buf, pnanovdb_uint32_t grid_type, pnanovdb_readaccessor_t acc)
{
	pnanovdb_address_t address = pnanovdb_readaccessor_get_value_address(grid_type, buf, acc, ijk);

	return float3(
		pnanovdb_read_float(buf, pnanovdb_address_offset(address, 0u)),
		pnanovdb_read_float(buf, pnanovdb_address_offset(address, 4u)),
		pnanovdb_read_float(buf, pnanovdb_address_offset(address, 8u)));
}

float3 ReadValueVec3f(pnanovdb_vec3_t pos, pnanovdb_buf_t buf, pnanovdb_uint32_t grid_type, pnanovdb_readaccessor_t acc)
{
	pnanovdb_coord_t ijk = pnanovdb_hdda_pos_to_ijk(pos);
	return ReadValueVec3f(ijk, buf, grid_type, acc);
}

float4 ReadValueVec4f(pnanovdb_coord_t ijk, pnanovdb_buf_t buf, pnanovdb_uint32_t grid_type, pnanovdb_readaccessor_t acc)
{
	pnanovdb_address_t address = pnanovdb_readaccessor_get_value_address(grid_type, buf, acc, ijk);

	return float4(
		pnanovdb_read_float(buf, pnanovdb_address_offset(address, 0u)),
		pnanovdb_read_float(buf, pnanovdb_address_offset(address, 4u)),
		pnanovdb_read_float(buf, pnanovdb_address_offset(address, 8u)),
		pnanovdb_read_float(buf, pnanovdb_address_offset(address, 12u)));
}

// Faster, but float32 only
float ReadValueF32(pnanovdb_coord_t ijk, pnanovdb_buf_t buf, pnanovdb_readaccessor_t acc)
{
	pnanovdb_address_t address = pnanovdb_readaccessor_get_value_address(PNANOVDB_GRID_TYPE_FLOAT, buf, acc, ijk);
	return pnanovdb_read_float(buf, address);
}

float ReadValueF32(pnanovdb_vec3_t pos, pnanovdb_buf_t buf, pnanovdb_readaccessor_t acc)
{
	pnanovdb_coord_t ijk = pnanovdb_hdda_pos_to_ijk(pos);
	return ReadValueF32(ijk, buf, acc);
}

float TrilinearSampling(pnanovdb_vec3_t pos, pnanovdb_buf_t buf, pnanovdb_uint32_t grid_type, pnanovdb_readaccessor_t acc)
{
	pnanovdb_coord_t ijk = pnanovdb_hdda_pos_to_ijk(pos);
	pnanovdb_vec3_t uvw = pos - ijk;

	float Values[2][2][2];
	Values[0][0][0] = ReadValue(ijk + pnanovdb_coord_t(0, 0, 0), buf, grid_type, acc);
	Values[0][0][1] = ReadValue(ijk + pnanovdb_coord_t(0, 0, 1), buf, grid_type, acc);
	Values[0][1][1] = ReadValue(ijk + pnanovdb_coord_t(0, 1, 1), buf, grid_type, acc);
	Values[0][1][0] = ReadValue(ijk + pnanovdb_coord_t(0, 1, 0), buf, grid_type, acc);
	Values[1][0][0] = ReadValue(ijk + pnanovdb_coord_t(1, 0, 0), buf, grid_type, acc);
	Values[1][0][1] = ReadValue(ijk + pnanovdb_coord_t(1, 0, 1), buf, grid_type, acc);
	Values[1][1][1] = ReadValue(ijk + pnanovdb_coord_t(1, 1, 1), buf, grid_type, acc);
	Values[1][1][0] = ReadValue(ijk + pnanovdb_coord_t(1, 1, 0), buf, grid_type, acc);

	return lerp(
		lerp(
			lerp(Values[0][0][0], Values[0][0][1], uvw[2]), 
			lerp(Values[0][1][0], Values[0][1][1], uvw[2]), 
		uvw[1]),
		lerp(
			lerp(Values[1][0][0], Values[1][0][1], uvw[2]), 
			lerp(Values[1][1][0], Values[1][1][1], uvw[2]), 
		uvw[1]), 
	uvw[0]);
}

float3 TrilinearSamplingVec3f(pnanovdb_vec3_t pos, pnanovdb_buf_t buf, pnanovdb_uint32_t grid_type, pnanovdb_readaccessor_t acc)
{
	pnanovdb_coord_t ijk = pnanovdb_hdda_pos_to_ijk(pos);
	pnanovdb_vec3_t uvw = pos - ijk;

	float3 Values[2][2][2];
	Values[0][0][0] = ReadValueVec3f(ijk + pnanovdb_coord_t(0, 0, 0), buf, grid_type, acc);
	Values[0][0][1] = ReadValueVec3f(ijk + pnanovdb_coord_t(0, 0, 1), buf, grid_type, acc);
	Values[0][1][1] = ReadValueVec3f(ijk + pnanovdb_coord_t(0, 1, 1), buf, grid_type, acc);
	Values[0][1][0] = ReadValueVec3f(ijk + pnanovdb_coord_t(0, 1, 0), buf, grid_type, acc);
	Values[1][0][0] = ReadValueVec3f(ijk + pnanovdb_coord_t(1, 0, 0), buf, grid_type, acc);
	Values[1][0][1] = ReadValueVec3f(ijk + pnanovdb_coord_t(1, 0, 1), buf, grid_type, acc);
	Values[1][1][1] = ReadValueVec3f(ijk + pnanovdb_coord_t(1, 1, 1), buf, grid_type, acc);
	Values[1][1][0] = ReadValueVec3f(ijk + pnanovdb_coord_t(1, 1, 0), buf, grid_type, acc);

	return lerp(
		lerp(
			lerp(Values[0][0][0], Values[0][0][1], uvw[2]),
			lerp(Values[0][1][0], Values[0][1][1], uvw[2]),
			uvw[1]),
		lerp(
			lerp(Values[1][0][0], Values[1][0][1], uvw[2]),
			lerp(Values[1][1][0], Values[1][1][1], uvw[2]),
			uvw[1]),
		uvw[0]);
}

float TrilinearSampling(float Step, VdbRay ray, pnanovdb_buf_t buf, pnanovdb_uint32_t grid_type, pnanovdb_readaccessor_t acc)
{
	pnanovdb_vec3_t pos = pnanovdb_hdda_ray_start(ray.Origin, Step, ray.Direction);
	return TrilinearSampling(pos, buf, grid_type, acc);
}

bool CheckBounds(inout VdbRay Ray, pnanovdb_vec3_t bbox_min, pnanovdb_vec3_t bbox_max)
{
	return pnanovdb_hdda_ray_clip(bbox_min, bbox_max, Ray.Origin, Ray.TMin, Ray.Direction, Ray.TMax);
}

float3 WorldToIndexDirection(float3 WorldDirection, float4x4 WorldToLocal, pnanovdb_buf_t buf, pnanovdb_grid_handle_t grid)
{
	float3 Dir = mul(float4(WorldDirection, 0.0), WorldToLocal).xyz;
	return normalize(pnanovdb_grid_world_to_index_dirf(buf, grid, Dir));
}

float3 WorldToIndexPosition(float3 WorldPos, float4x4 WorldToLocal, pnanovdb_buf_t buf, pnanovdb_grid_handle_t grid)
{
	float3 Pos = mul(float4(WorldPos, 1.0), WorldToLocal).xyz;
	return pnanovdb_grid_world_to_indexf(buf, grid, Pos);
}

float3 LocalToIndexPosition(float3 LocalPos, pnanovdb_buf_t buf, pnanovdb_grid_handle_t grid)
{
	return pnanovdb_grid_world_to_indexf(buf, grid, LocalPos);
}

float3 IndexToWorldDirection(float3 IndexDirection, float4x4 LocalToWorld, pnanovdb_buf_t buf, pnanovdb_grid_handle_t grid)
{
	float3 LocalDir = pnanovdb_grid_index_to_world_dirf(buf, grid, IndexDirection);
	float3 WorldDir = mul(float4(LocalDir, 0.0), LocalToWorld).xyz;
	return normalize(WorldDir);
}

float3 IndexToWorldPosition(float3 IndexPos, float4x4 LocalToWorld, pnanovdb_buf_t buf, pnanovdb_grid_handle_t grid)
{
	float3 LocalPos = pnanovdb_grid_index_to_worldf(buf, grid, IndexPos);
	return mul(float4(LocalPos, 1.0), LocalToWorld).xyz;
}

float IndexToWorldDistance(float3 IndexVec, float4x4 LocalToWorld, pnanovdb_buf_t buf, pnanovdb_grid_handle_t grid)
{
	float3 LocalVec = pnanovdb_grid_index_to_world_dirf(buf, grid, IndexVec);
	float3 WorldVec = mul(float4(LocalVec, 0.0), LocalToWorld).xyz;
	return length(WorldVec);
}


VdbRay PrepareRayFromPixel(pnanovdb_buf_t grid_buf, pnanovdb_grid_handle_t grid, float3 StartPos, float3 EndPos)
{
	Segment Seg;
    Seg.Start = StartPos;
    Seg.End = EndPos;
	
	// Index space
	float3 Origin = LocalToIndexPosition(Seg.Start, grid_buf, grid);
	float3 End = LocalToIndexPosition(Seg.End, grid_buf, grid);

	float Dist = length(End - Origin);

	VdbRay Ray;
	Ray.Origin = Origin;
	Ray.Direction = (End - Origin) / Dist;
	Ray.TMin = 0.0001f;
	Ray.TMax = Dist;

	return Ray;
}

//-----------------------------------------------------------------------------------------------------------
// Level Set specific
//-----------------------------------------------------------------------------------------------------------

struct ZeroCrossingHit
{
	float t_hit;
	float v0;
	pnanovdb_coord_t ijk_hit;
};

pnanovdb_vec3_t ZeroCrossingNormal(pnanovdb_uint32_t grid_type, pnanovdb_buf_t grid_buf, pnanovdb_readaccessor_t acc, in ZeroCrossingHit ZCH)
{
	pnanovdb_coord_t ijk = ZCH.ijk_hit;
	pnanovdb_vec3_t iNormal = -ZCH.v0.xxx;

	ijk.x += 1;
	iNormal.x += ReadValue(ijk, grid_buf, grid_type, acc);

	ijk.x -= 1;
	ijk.y += 1;
	iNormal.y += ReadValue(ijk, grid_buf, grid_type, acc);

	ijk.y -= 1;
	ijk.z += 1;
	iNormal.z += ReadValue(ijk, grid_buf, grid_type, acc);

	return normalize(iNormal);
}

pnanovdb_bool_t GetNextIntersection(
	VdbSampler LsSampler,
	in out VdbRay iRay,
	in out ZeroCrossingHit HitResults)
{
    return pnanovdb_hdda_zero_crossing(
        LsSampler.GridType,
        LsSampler.GridBuffer,
        LsSampler.Accessor,
        iRay.Origin, iRay.TMin,
        iRay.Direction, iRay.TMax,
        HitResults.t_hit,
        HitResults.v0
    );
}

//-----------------------------------------------------------------------------------------------------------
// Fog Volume specific
//-----------------------------------------------------------------------------------------------------------

float DeltaTracking(in VdbRay Ray, pnanovdb_buf_t buf, pnanovdb_uint32_t grid_type, pnanovdb_readaccessor_t acc, HeterogenousMedium medium, inout RandomSequence RandSequence)
{
	float densityMaxInv = 1.0f / medium.densityMax;
	float t = Ray.TMin;
	pnanovdb_vec3_t pos;

	do {
		t += -log(RandomSequence_GenerateSample1D(RandSequence)) * densityMaxInv;
		pos = pnanovdb_hdda_ray_start(Ray.Origin, t, Ray.Direction);
	} while (t < Ray.TMax && ReadValue(pos, buf, grid_type, acc) * medium.densityScale * densityMaxInv < RandomSequence_GenerateSample1D(RandSequence));

	return t;
}

pnanovdb_vec3_t sampleHG(float g, float e1, float e2)
{
	// phase function.
	if (g == 0) {
		// isotropic
		const float phi = (2.0f * PI) * e1;
		const float cosTheta = 1.0f - 2.0f * e2;
		const float sinTheta = sqrt(1.0f - cosTheta * cosTheta);
		return pnanovdb_vec3_t(cos(phi) * sinTheta, sin(phi) * sinTheta, cosTheta);
	}
	else {
		const float phi = (2.0f * PI) * e2;
		const float s = 2.0f * e1 - 1.0f;
		const float denom = max(0.001f, (1.0f + g * s));
		const float f = (1.0f - g * g) / denom;
		const float cosTheta = 0.5f * (1.0f / g) * (1.0f + g * g - f * f);
		const float sinTheta = sqrt(1.0f - cosTheta * cosTheta);
		return pnanovdb_vec3_t(cos(phi) * sinTheta, sin(phi) * sinTheta, cosTheta);
	}
}

float PhaseHG(float CosTheta, float g)
{
	float denom = 1.0 + g * g - 2.0 * g * CosTheta;
	return 1.0 / (4.0 * PI) * (1.0 - g * g) / (denom * sqrt(denom));
}
// From NanoVDB samples
float GetTransmittance(
	pnanovdb_vec3_t bbox_min,
	pnanovdb_vec3_t bbox_max,
	VdbRay ray,
	VdbSampler sampler,
	HeterogenousMedium medium,
	float StepMultiplier = 1.0f )
{
	pnanovdb_bool_t hit = pnanovdb_hdda_ray_clip(bbox_min, bbox_max, ray.Origin, ray.TMin, ray.Direction, ray.TMax);
	if (!hit)
		return 1.0f;

	float densityMaxInv = 1.0f / medium.densityMax;
	
	float transmittance = 1.f;
	float t = ray.TMin;

	while (true) 
	{
		// Nearest strides have more detail
		StepMultiplier *= 2;
		const float densityMaxInvMultStep = densityMaxInv * StepMultiplier;

		t += densityMaxInvMultStep;
		if (t >= ray.TMax)
			break;

		float density = ReadValue(ray.Origin + t * ray.Direction, sampler.GridBuffer, sampler.GridType, sampler.Accessor) * medium.densityScale;

		transmittance *= 1.0f - density * densityMaxInv * densityMaxInvMultStep;
		if (transmittance < 0.1f)
			return 0.f;
	}
	return transmittance;
}

float GetTransmittanceHDDA(
    pnanovdb_vec3_t bbox_min,
    pnanovdb_vec3_t bbox_max,
    VdbRay ray,
    VdbSampler sampler,
    HeterogenousMedium medium)
{
    // Early exit if ray misses grid bounds
    if (!pnanovdb_hdda_ray_clip(bbox_min, bbox_max, ray.Origin, ray.TMin, ray.Direction, ray.TMax))
        return 1.0f;

    // Initialize HDDA traversal
    pnanovdb_hdda_t hdda;
    pnanovdb_hdda_init(hdda, ray.Origin, ray.TMin, ray.Direction, ray.TMax, 4);

    float transmittance = 1.0f;
    float densityMaxInv = 1.0f / medium.densityMax;

    // Step through the volume using HDDA
    while (pnanovdb_hdda_step(hdda))
    {
        // Get current position and cell coordinates
        pnanovdb_vec3_t pos = pnanovdb_hdda_ray_start(ray.Origin, hdda.tmin + 0.0001f, ray.Direction);
        pnanovdb_coord_t ijk = pnanovdb_hdda_pos_to_ijk(PNANOVDB_REF(pos));
        int dim = pnanovdb_uint32_as_int32(pnanovdb_readaccessor_get_dim(PNANOVDB_GRID_TYPE_FLOAT, sampler.GridBuffer, sampler.Accessor, PNANOVDB_REF(ijk)));

        // Update HDDA dimension and skip inactive or large cells
        pnanovdb_hdda_update(PNANOVDB_REF(hdda), ray.Origin, ray.Direction, dim);
        if (hdda.dim > 1 || !pnanovdb_readaccessor_is_active(sampler.GridType, sampler.GridBuffer, sampler.Accessor, PNANOVDB_REF(ijk)))
            continue;

        // Calculate density at current position
        float density = ReadValue(pos, sampler.GridBuffer, sampler.GridType, sampler.Accessor) * medium.densityScale;
        
        // Calculate step size using HDDA traversal distances
        float dt = pnanovdb_grid_get_voxel_size(sampler.GridBuffer, sampler.Grid, hdda.dim)  * medium.densityScale;
        
        // Update transmittance
        transmittance *= exp(-density * dt);

        // Early exit if transmittance is too low
        if (transmittance < 0.2f)
            return 0.0f;
    }

    return transmittance;
}

// Cf FLinearColor::MakeFromColorTemperature
float3 ColorTemperatureToRGB(float Temp)
{
	if (Temp < 1000.0f)
		return float3(0.0f, 0.0f, 0.0f);

	Temp = clamp(Temp, 1000.0f, 15000.0f);

	// Approximate Planckian locus in CIE 1960 UCS
	float u = (0.860117757f + 1.54118254e-4f * Temp + 1.28641212e-7f * Temp * Temp) / (1.0f + 8.42420235e-4f * Temp + 7.08145163e-7f * Temp * Temp);
	float v = (0.317398726f + 4.22806245e-5f * Temp + 4.20481691e-8f * Temp * Temp) / (1.0f - 2.89741816e-5f * Temp + 1.61456053e-7f * Temp * Temp);

	float x = 3.0f * u / (2.0f * u - 8.0f * v + 4.0f);
	float y = 2.0f * v / (2.0f * u - 8.0f * v + 4.0f);
	float z = 1.0f - x - y;

	float Y = 1.0f;
	float X = Y / y * x;
	float Z = Y / y * z;

	// XYZ to RGB with BT.709 primaries
	float R = 3.2404542f * X + -1.5371385f * Y + -0.4985314f * Z;
	float G = -0.9692660f * X + 1.8760108f * Y + 0.0415560f * Z;
	float B = 0.0556434f * X + -0.2040259f * Y + 1.0572252f * Z;

	return float3(R, G, B);
}

float Average(float3 Value)
{
	return dot(Value, 1.0/3.0);
}

// Phase function.
pnanovdb_vec3_t SampleHenyeyGreenstein(float g, float e1, float e2)
{
	float cosTheta = 1.0f - 2.0f * e2; // isotropic

	if (abs(g) >= 0.001)  // anisotropic
	{
		float sqrTerm = (1.0 - g * g) / (1.0 - g + 2.0 * g * e1);
		cosTheta = (1.0 + g * g - sqrTerm * sqrTerm) / (2.0 * g);
	}

	float sinTheta = sqrt(max(0.000001, 1.0f - cosTheta * cosTheta));
	float phi = (2.0f * PI) * e1;
	return pnanovdb_vec3_t(cos(phi) * sinTheta, sin(phi) * sinTheta, cosTheta);
}

#endif // D_VDB_VOLUMETRIC_COMMON
