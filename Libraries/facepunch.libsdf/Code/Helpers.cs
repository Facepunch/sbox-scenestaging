using System;

namespace Sandbox.Sdf;

internal static class Helpers
{
	public static Vector2 NormalizeSafe( in Vector2 vec )
	{
		var length = vec.Length;

		if ( length > 9.9999997473787516E-06 )
		{
			return vec / length;
		}
		else
		{
			return 0f;
		}
	}

	public static Vector2 Rotate90( Vector2 v )
	{
		return new Vector2( v.y, -v.x );
	}

	public static Vector3 RotateNormal( Vector3 oldNormal, float sin, float cos )
	{
		var normal2d = new Vector2( oldNormal.x, oldNormal.y );

		if ( normal2d.LengthSquared <= 0.000001f )
		{
			return oldNormal;
		}

		normal2d = NormalizeSafe( normal2d );

		return new Vector3( normal2d.x * cos, normal2d.y * cos, sin ).Normal;
	}

	public static float GetEpsilon( Vector2 vec, float frac = 0.0001f )
	{
		return Math.Max( Math.Abs( vec.x ), Math.Abs( vec.y ) ) * frac;
	}

	public static float GetEpsilon( Vector2 a, Vector2 b, float frac = 0.0001f )
	{
		return Math.Max( GetEpsilon( a, frac ), GetEpsilon( b, frac ) );
	}

	public static int NextPowerOf2( int value )
	{
		var po2 = 1;
		while ( po2 < value ) po2 <<= 1;

		return po2;
	}
}
