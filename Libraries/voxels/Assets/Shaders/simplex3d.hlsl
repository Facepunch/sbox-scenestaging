//	--------------------------------------------------------------------
//	Optimized implementation of simplex noise.
//	Based on stegu's simplex noise: https://github.com/stegu/webgl-noise.
//	Contact : atyuwen@gmail.com
//	Author : Yuwen Wu (https://atyuwen.github.io/)
//	License : Distributed under the MIT License.
//	--------------------------------------------------------------------

// Permuted congruential generator (only top 16 bits are well shuffled).
// References: 1. Mark Jarzynski and Marc Olano, "Hash Functions for GPU Rendering".
//             2. UnrealEngine/Random.ush. https://github.com/EpicGames/UnrealEngine

uint pcg3d16(uint3 p)
{
	uint3 v = p * 1664525u + 1013904223u;
	v.x += v.y*v.z; v.y += v.z*v.x; v.z += v.x*v.y;
	v.x += v.y*v.z;
	return v.x;
}
uint pcg4d16(uint4 p)
{
	uint4 v = p * 1664525u + 1013904223u;
	v.x += v.y*v.w; v.y += v.z*v.x; v.z += v.x*v.y; v.w += v.y*v.z;
	v.x += v.y*v.w;
	return v.x;
}

// Get random gradient from hash value.
float3 gradient3d(uint hash)
{
	float3 g = float3(hash.xxx & uint3(0x80000, 0x40000, 0x20000));
	return g * float3(1.0 / 0x40000, 1.0 / 0x20000, 1.0 / 0x10000) - 1.0;
}
float4 gradient4d(uint hash)
{
	float4 g = float4(hash.xxxx & uint4(0x80000, 0x40000, 0x20000, 0x10000));
	return g * float4(1.0 / 0x40000, 1.0 / 0x20000, 1.0 / 0x10000, 1.0 / 0x8000) - 1.0;
}

// 3D Simplex Noise. Approximately 71 instruction slots used.
// Assume p is in the range [-32768, 32767].
float SimplexNoise3D(float3 p)
{
	const float2 C = float2(1.0 / 6.0, 1.0 / 3.0);
	const float4 D = float4(0.0, 0.5, 1.0, 2.0);

	// First corner
	float3 i = floor(p + dot(p, C.yyy));
	float3 x0 = p - i + dot(i, C.xxx);

	// Other corners
	float3 g = step(x0.yzx, x0.xyz);
	float3 l = 1.0 - g;
	float3 i1 = min(g.xyz, l.zxy);
	float3 i2 = max(g.xyz, l.zxy);

	// x0 = x0 - 0.0 + 0.0 * C.xxx;
	// x1 = x0 - i1  + 1.0 * C.xxx;
	// x2 = x0 - i2  + 2.0 * C.xxx;
	// x3 = x0 - 1.0 + 3.0 * C.xxx;
	float3 x1 = x0 - i1 + C.xxx;
	float3 x2 = x0 - i2 + C.yyy; // 2.0*C.x = 1/3 = C.y
	float3 x3 = x0 - D.yyy;      // -1.0+3.0*C.x = -0.5 = -D.y

	i = i + 32768.5;
	uint hash0 = pcg3d16((uint3)i);
	uint hash1 = pcg3d16((uint3)(i + i1));
	uint hash2 = pcg3d16((uint3)(i + i2));
	uint hash3 = pcg3d16((uint3)(i + 1 ));

	float3 p0 = gradient3d(hash0);
	float3 p1 = gradient3d(hash1);
	float3 p2 = gradient3d(hash2);
	float3 p3 = gradient3d(hash3);

	// Mix final noise value.
	float4 m = saturate(0.5 - float4(dot(x0, x0), dot(x1, x1), dot(x2, x2), dot(x3, x3)));
	float4 mt = m * m;
	float4 m4 = mt * mt;
	return 62.6 * dot(m4, float4(dot(x0, p0), dot(x1, p1), dot(x2, p2), dot(x3, p3)));
}

float FractalSimplexNoise3D(float3 pos, int octaves)
{
    float value = 0.0;
    float scale = 0.5;

    for (int i = 0; i < octaves; i++)
    {
        value += SimplexNoise3D(pos) * scale;
        pos *= 2.0;
        scale *= 0.5;
    }

    return value;
}
