HEADER
{
	Description = "Standard shader but it deforms splines";
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

	// TODO can be packed more efficiently
	float4 P1 < Attribute("P1"); >;
	float4 P2 < Attribute("P2"); >;
	float4 P3 < Attribute("P3"); >;
	float4 StartEndRoll < Attribute("RollStartEnd"); >;
	float4 StartEndWidthHeightScale < Attribute("WidthHeightScaleStartEnd"); >;

	float MinInModelDir < Attribute("MinInModelDir"); >;
	float SizeInModelDir < Attribute("SizeInModelDir"); >;
	float4 ModelRotation < Attribute("ModelRotation"); >;

	float3 VectorRotate( float3 v, float4 q )
	{
		// common factors
		float x2 = q.x + q.x;	// 2x
		float y2 = q.y + q.y;	// 2y
		float z2 = q.z + q.z;	// 2z
		float xx = q.x * x2;	// 2x^2
		float xy = q.x * y2;	// 2xy
		float xz = q.x * z2;	// 2xz
		float yy = q.y * y2;	// 2y^2
		float yz = q.y * z2;	// 2yz
		float zz = q.z * z2;	// 2z^2
		float wx = q.w * x2;	// 2xw
		float wy = q.w * y2;	// 2yw
		float wz = q.w * z2;	// 2zw

		return float3(
			dot( v, float3( 1.0 - ( yy + zz ), xy - wz, xz + wy ) ),
			dot( v, float3( xy + wz, 1.0 - ( xx + zz ), yz - wx ) ),
			dot( v, float3( xz - wy, yz + wx, 1.0 - ( xx + yy ) ) ) );
	}

	// calculate how far along we are in the bezier curve
	float CalculateBezierT(float3 vertLocalPos, float minForwad, float sizeForward)
	{
		float distanceAlongX = vertLocalPos.x - minForwad;
		float t = distanceAlongX / sizeForward;

		return t;
	}

	float3 CalculateBezierPosition(float t, float3 p0, float3 p1, float3 p2, float3 p3)
	{
		// Calculate value for cubic Bernstein polynominal
		// B(t) = (1 - t)^3 * P0 + 3 * (1 - t)^2 * t * P1 + 3 * (1 - t)^2 * t^2 * P2 + t^3 * P3
		float tSquare = t * t;
		float tCubic = tSquare * t;
		float oneMinusT = 1 - t;
		float oneMinusTSquare = oneMinusT * oneMinusT;
		float oneMinusTCubic = oneMinusTSquare * oneMinusT;

		float w0 = oneMinusTCubic; // -t^3 + 3t^2 - 3t + 1
		float w1 = 3 * oneMinusTSquare * t; // 3t^3 - 6t^2 + 3t
		float w2 = 3 * oneMinusT * tSquare; // -3t^3 + 3t^2
		float w3 = tCubic; // t^3

		float3 weightedP0 = w0 * p0;
		float3 weightedP1 = w1 * p1;
		float3 weightedP2 = w2 * p2;
		float3 weightedP3 = w3 * p3;

		return weightedP0 + weightedP1 + weightedP2 + weightedP3;
	}

	float3 CalculateBezierTangent(float t, float3 p0, float3 p1, float3 p2, float3 p3)
	{
		// Calculate the derivative of the cubic Bernstein polynominal
		// B'(t) = 3 * (1 - t) ^2 * (P1 - P0) + 6 * (1 - t) * t * (P2 - P1) + 3 * t^2 * (P3 - P2)
		float t2 = t * t;

		float w0 = -3 * t2 + 6 * t - 3;
		float w1 = 9 * t2 - 12 * t + 3;
		float w2 = -9 * t2 + 6 * t;
		float w3 = 3 * t2;

		float3 weightedP0 = w0 * p0;
		float3 weightedP1 = w1 * p1;
		float3 weightedP2 = w2 * p2;
		float3 weightedP3 = w3 * p3;

		return weightedP0 + weightedP1 + weightedP2 + weightedP3;
	}

	float CalculateRoll(float t, float rollStart, float rollEnd)
	{
		return lerp(rollStart, rollEnd, t);
	}

	float2 CalculateScale(float t, float2 startScaleWidthHeight, float2 endScaleWidthHeight)
	{
		return lerp(startScaleWidthHeight, endScaleWidthHeight, t);
	}

	float3 ScaleAndRotateVector(float t, float3 vertLocalPos, float2 scale, float3 right, float3 up)
	{
		float3 scaledRight = right * scale.x;
		float3 scaledUp = up * scale.y;

		// only modify the Y and Z components
		// X will only be modified by the spline location
		return vertLocalPos.y * scaledRight + vertLocalPos.z * scaledUp;
	}

	float3 RotateNormal(float3 normal, float3 forward, float3 right, float3 up)
	{
		return normal.x * forward + normal.y * right + normal.z * up;
	}

	PixelInput MainVs( VertexInput i )
	{
		float3 rotatedLocalPos = VectorRotate(i.vPositionOs, ModelRotation);
		float t = CalculateBezierT(rotatedLocalPos, MinInModelDir, SizeInModelDir);

		float3 p0 = float3(0, 0, 0);
		float3 p1 = P1.xyz;
		float3 p2 = P2.xyz;
		float3 p3 = P3.xyz;

		float rollStart = StartEndRoll.x;
		float rollEnd = StartEndRoll.y;

		float roll = CalculateRoll(t, rollStart, rollEnd);

		// Maybbbeee we want to expose this in the future
		float2 startScaleWidthHeight = StartEndWidthHeightScale.xy;
		float2 endScaleWidthHeight = StartEndWidthHeightScale.zw;

		float2 scale = CalculateScale(t, startScaleWidthHeight, endScaleWidthHeight);

		float3 up = float3(0, 0, 1); // TODO make up axis configurable
		float3 forward = CalculateBezierTangent(t, p0, p1, p2, p3);
		float3 right = normalize(cross(up, forward));
		up = normalize( cross(forward, right) );

		float sine;
		float cosine;

		sincos(roll, sine, cosine);
		float3 rightRotated = cosine * right - sine * up;
		float3 upRotated = sine * right + cosine * up;

		float3 curvePosition = CalculateBezierPosition(t, p0, p1, p2, p3);
		float3 deformedPosition = ScaleAndRotateVector(t, rotatedLocalPos, scale, rightRotated, upRotated);

		// Deform position
		i.vPositionOs = curvePosition + deformedPosition;

		PixelInput o = ProcessVertex( i );

		float3 vNormalOs;
		float4 vTangentUOs_flTangentVSign;
		float3x4 matObjectToWorld = CalculateInstancingObjectToWorldMatrix( i );

		VS_DecodeObjectSpaceNormalAndTangent( i, vNormalOs, vTangentUOs_flTangentVSign );

		// Deform normal
		float3 rotatedNormal = VectorRotate(vNormalOs, ModelRotation);
		vNormalOs = RotateNormal(rotatedNormal, forward, rightRotated, upRotated);

		#if ( S_MODE_TOOLS_VIS )
		{
			float3x3 matInvTranspose = ComputeInverseTranspose( matObjectToWorld );
			o.vNormalWs.xyz = normalize( mul( matInvTranspose, vNormalOs.xyz ) );
		}
		#else
		{
			o.vNormalWs.xyz = normalize( mul( matObjectToWorld, float4( vNormalOs.xyz, 0.0 ) ) );
		}
		#endif

		#ifdef PS_INPUT_HAS_TANGENT_BASIS 
		{
			float3 vTangentUWs;
			#if ( S_MODE_TOOLS_VIS )
			{
				float3x3 matInvTranspose = ComputeInverseTranspose( matObjectToWorld );
				vTangentUWs = mul( matInvTranspose, vTangentUOs_flTangentVSign.xyz );
			}
			#else
			{
				vTangentUWs = mul( matObjectToWorld, float4( vTangentUOs_flTangentVSign.xyz, 0.0 ) );
			}
			#endif

			//
			// Force tangentU perpendicular to normal and normalize
			//
			vTangentUWs.xyz = normalize( vTangentUWs.xyz - ( o.vNormalWs.xyz * dot( vTangentUWs.xyz, o.vNormalWs.xyz ) ) );

			o.vTangentUWs.xyz = vTangentUWs.xyz;
			o.vTangentVWs.xyz = cross( o.vNormalWs.xyz, vTangentUWs.xyz ) * vTangentUOs_flTangentVSign.w;
		}
		#endif

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

		//return i.vVertexColor;
		return ShadingModelStandard::Shade( i, m );
	}
}
