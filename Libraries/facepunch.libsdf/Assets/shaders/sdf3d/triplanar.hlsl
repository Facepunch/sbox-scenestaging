#include "common/pixel.hlsl"

float3 RnmBlendUnpacked(float3 n1, float3 n2)
{
	n1 += float3( 0,  0, 1);
	n2 *= float3(-1, -1, 1);
	return n1*dot(n1, n2)/n1.z - n2;
}

Material ToMaterialTriplanar( in PixelInput i, in Texture2D tColor, in Texture2D tNormal, in Texture2D tRma )
{
#ifdef TRIPLANAR_OBJECT_SPACE
	float3 worldPos = i.vPositionOs.xyz / 256.0;
#else
	float3 worldPos = (i.vPositionWithOffsetWs.xyz + g_vCameraPositionWs.xyz) / 256.0;
#endif

	float2 uvX = worldPos.zy;
	float2 uvY = worldPos.xz;
	float2 uvZ = worldPos.xy;

	float3 triblend = saturate(pow(abs(i.vNormalOs.xyz), 4));
	triblend /= max(dot(triblend, half3(1,1,1)), 0.0001);

	half3 absVertNormal = abs(i.vNormalOs);
	half3 axisSign = i.vNormalOs < 0 ? -1 : 1;

	uvX.x *= axisSign.x;
	uvY.x *= axisSign.y;
	uvZ.x *= -axisSign.z;

	float4 colX = Tex2DS( tColor, TextureFiltering, uvX );
	float4 colY = Tex2DS( tColor, TextureFiltering, uvY );
	float4 colZ = Tex2DS( tColor, TextureFiltering, uvZ );
	float4 col = colX * triblend.x + colY * triblend.y + colZ * triblend.z;

	float3 tnormalX = DecodeNormal(Tex2DS( tNormal, TextureFiltering, uvX ).xyz);
	float3 tnormalY = DecodeNormal(Tex2DS( tNormal, TextureFiltering, uvY ).xyz);
	float3 tnormalZ = DecodeNormal(Tex2DS( tNormal, TextureFiltering, uvZ ).xyz);

	tnormalX.x *= axisSign.x;
	tnormalY.x *= axisSign.y;
	tnormalZ.x *= -axisSign.z;

	tnormalX = half3(tnormalX.xy + i.vNormalWs.zy, i.vNormalWs.x);
	tnormalY = half3(tnormalY.xy + i.vNormalWs.xz, i.vNormalWs.y);
	tnormalZ = half3(tnormalZ.xy + i.vNormalWs.xy, i.vNormalWs.z);

	// Triblend normals and add to world normal
	float3 norm = normalize(
		tnormalX.zyx * triblend.x +
		tnormalY.xzy * triblend.y +
		tnormalZ.xyz * triblend.z +
		i.vNormalWs
	);

	float4 rmaX = Tex2DS( tRma, TextureFiltering, uvX );
	float4 rmaY = Tex2DS( tRma, TextureFiltering, uvY );
	float4 rmaZ = Tex2DS( tRma, TextureFiltering, uvZ );
	float4 rma = rmaX * triblend.x + rmaY * triblend.y + rmaZ * triblend.z;

	Material m = Material::From( i, col, float4( 0.5, 0.5, 1.0, 1.0 ), rma, g_flTintColor );

	m.Normal = norm;

	return m;
}
