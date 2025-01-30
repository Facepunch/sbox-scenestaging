namespace Sandbox;
using System;

// TODO tests
public static partial class MathY
{
	// Solve the normal form x^2 + Ax + B = 0 for real roots. 
	private static List<float> QuadraticRoots( float a, float b )
	{
		float discriminant = 0.25f * a * a - b;
		if ( discriminant >= 0.0f )
		{
			var sqrtDiscriminant = MathF.Sqrt( discriminant );
			var r0 = -0.5f * a - sqrtDiscriminant;
			var r1 = -0.5f * a + sqrtDiscriminant;

			return new List<float> { r0, r1 };
		}

		return new List<float>();
	}

	// Solve the normal form x^3 + Ax^2 + Bx + C = 0 for real roots.
	// Adapted from https://github.com/erich666/GraphicsGems/blob/master/gems/Roots3And4.c
	private static List<float> CubicRoots( float a, float b, float c )
	{
		/*  substitute x = y - A/3 to eliminate quadric term: x^3 +px + q = 0 */
		float squareA = a * a;
		float p = 1.0f / 3 * (-1.0f / 3 * squareA + b);
		float q = 1.0f / 2 * (2.0f / 27 * a * squareA - 1.0f / 3 * a * b + c);
		float cubicP = p * p * p;
		float squareQ = q * q;
		float discriminant = squareQ + cubicP;

		float sub = 1.0f / 3 * a;

		if ( MathF.Abs( discriminant ).AlmostEqual( 0.0f ) )
		{

			if ( MathF.Abs( q ).AlmostEqual( 0.0f ) )
			{
				// One real root.
				return new List<float> { 0.0f - sub };
			}
			else
			{
				// One single and one double root.
				float U = MathF.Cbrt( -q );
				return new List<float> { 2.0f * U - sub, -U - sub };
			}
		}
		else if ( discriminant < 0 )
		{
			// Casus irreducibilis: three real solutions
			float phi = 1.0f / 3 * MathF.Acos( -q / MathF.Sqrt( -cubicP ) );
			float t = 2.0f * MathF.Sqrt( -p );

			return new List<float>
			{
				t * MathF.Cos( phi ) - sub,
				-t * MathF.Cos( phi + MathF.PI / 3  ) - sub,
				-t * MathF.Cos( phi - MathF.PI / 3  ) - sub
			};
		}
		else
		{
			// One real solution
			float sqrtDicriminant = MathF.Sqrt( discriminant );
			float s = MathF.Cbrt( q - sqrtDicriminant );
			float t = -MathF.Cbrt( q + sqrtDicriminant );

			return new List<float> { s + t - sub };
		}
	}

	// Solve the equation Ax^2 + Bx + C = 0 for real roots. 
	public static List<float> SolveQuadratic( float a, float b, float c )
	{
		if ( MathF.Abs( a ).AlmostEqual( 0.0f ) )
		{
			// First coefficient is zero, so this is at most linear
			if ( MathF.Abs( b ).AlmostEqual( 0.0f ) )
			{
				// Second coefficient is also zero
				return new List<float>();
			}

			// Linear Bx + C = 0 and B != 0.
			return new List<float> { -c / b };
		}

		// normal form: Ax^2 + Bx + C = 0
		return QuadraticRoots( b / a, c / a );
	}

	// Solve the equation Ax^3 + Bx^2 + Cx + D = 0 for real roots. 
	public static List<float> SolveCubic( float a, float b, float c, float d )
	{
		if ( MathF.Abs( a ).AlmostEqual( 0.0f ) )
		{
			// Leading coefficient is zero, so this is at most quadratic
			var quadraticRoots = SolveQuadratic( b, c, d );
			return quadraticRoots;
		}

		// normal form: x^3 + Ax^2 + Bx + C = 0
		return CubicRoots( b / a, c / a, d / a );
	}
}
