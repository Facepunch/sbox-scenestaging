using Sandbox.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Sandbox.Solids;

internal readonly struct Simplex2 : IEquatable<Simplex2>, IComparable<Simplex2>
{
	public static Simplex2 Create( Vertex a, Vertex b )
	{
		if ( a.Equals( b ) )
		{
			throw new ArgumentException( "Vertices must be unique." );
		}

		return new Simplex2( a, b );
	}

	public Vertex A { get; }
	public Vertex B { get; }

	public Simplex2 Reverse => new( B, A );

	internal Simplex2( Vertex a, Vertex b )
	{
		A = a;
		B = b;
	}

	public bool Equals( Simplex2 other )
	{
		return A.Equals( other.A ) && B.Equals( other.B );
	}

	public override bool Equals( object? obj )
	{
		return obj is Simplex2 other && Equals( other );
	}

	public override int GetHashCode()
	{
		return HashCode.Combine( A, B );
	}

	public int CompareTo( Simplex2 other )
	{
		var aCompare = A.CompareTo( other.A );
		return aCompare != 0 ? aCompare : B.CompareTo( other.B );
	}
}

internal readonly struct Simplex3 : IEquatable<Simplex3>, IComparable<Simplex3>
{
	public static Simplex3 Create( Vertex a, Vertex b, Vertex c )
	{
		if ( a.Equals( b ) || a.Equals( c ) || b.Equals( c ) )
		{
			throw new ArgumentException( "Vertices must be unique." );
		}

		return new Simplex3( a, b, c );
	}

	public IntPlane Plane
	{
		get
		{
			var cross = Vector3Long.Cross( B - A, C - A );
			return new( cross, Vector3Long.Dot( cross, (Vector3Int)A ) );
		}
	}

	public Vertex A { get; }
	public Vertex B { get; }
	public Vertex C { get; }

	public Simplex2 AB => new Simplex2( A, B );
	public Simplex2 BC => new Simplex2( B, C );
	public Simplex2 CA => new Simplex2( C, A );

	public Simplex3 Reverse => new Simplex3( A, C, B );

	internal Simplex3( Vertex a, Vertex b, Vertex c )
	{
		if ( a.CompareTo( b ) < 0 && a.CompareTo( c ) < 0 )
		{
			A = a;
			B = b;
			C = c;
		}
		else if ( b.CompareTo( c ) < 0 )
		{
			A = b;
			B = c;
			C = a;
		}
		else
		{
			A = c;
			B = a;
			C = b;
		}
	}

	public Sign GetSide( Vertex point ) => Plane.GetSide( point );

	public Sign Intersects( Simplex3 other )
	{
		// Triangles intersect if an edge of one passes through the other

		var result = Sign.Positive;

		if ( SimplexExtensions.UpdateMin( ref result, Intersects( other.AB ) ) ) return result;
		if ( SimplexExtensions.UpdateMin( ref result, Intersects( other.BC ) ) ) return result;
		if ( SimplexExtensions.UpdateMin( ref result, Intersects( other.CA ) ) ) return result;

		if ( SimplexExtensions.UpdateMin( ref result, other.Intersects( AB ) ) ) return result;
		if ( SimplexExtensions.UpdateMin( ref result, other.Intersects( BC ) ) ) return result;
		if ( SimplexExtensions.UpdateMin( ref result, other.Intersects( CA ) ) ) return result;

		return result;
	}

	public Sign Intersects( Simplex2 line )
	{
		var plane = Plane;
		var sideA = plane.GetSide( line.A );
		var sideB = plane.GetSide( line.B );

		if ( sideA == Sign.Zero || sideB == Sign.Zero ) return Sign.Zero;

		if ( sideA == sideB ) return Sign.Positive;

		if ( sideA == Sign.Positive )
		{
			line = line.Reverse;
		}

		var result = Sign.Negative;

		if ( A == line.A || A == line.B ) return Sign.Zero;
		if ( B == line.A || B == line.B ) return Sign.Zero;
		if ( C == line.A || C == line.B ) return Sign.Zero;

		if ( SimplexExtensions.UpdateMax( ref result, Create( A, B, line.B ).GetSide( line.A ) ) ) return result;
		if ( SimplexExtensions.UpdateMax( ref result, Create( B, C, line.B ).GetSide( line.A ) ) ) return result;
		if ( SimplexExtensions.UpdateMax( ref result, Create( C, A, line.B ).GetSide( line.A ) ) ) return result;

		return result;
	}
	public bool Equals( Simplex3 other )
	{
		return A.Equals( other.A ) && B.Equals( other.B ) && C.Equals( other.C );
	}

	public override bool Equals( object? obj )
	{
		return obj is Simplex3 other && Equals( other );
	}

	public override int GetHashCode()
	{
		return HashCode.Combine( A, B, C );
	}

	public int CompareTo( Simplex3 other )
	{
		var aCompare = A.CompareTo( other.A );
		if ( aCompare != 0 ) return aCompare;

		var bCompare = B.CompareTo( other.B );
		if ( bCompare != 0 ) return bCompare;

		return C.CompareTo( other.C );
	}
}

public readonly record struct Vector3Long( long X, long Y, long Z )
{
	public static implicit operator Vector3Long( Vector3Int vector ) => new( vector.x, vector.y, vector.z );

	public static explicit operator Vector3Int( Vector3Long vector )
	{
		checked
		{
			return new Vector3Int( (int)vector.X, (int)vector.Y, (int)vector.Z );
		}
	}

	public static explicit operator Vertex( Vector3Long vector )
	{
		checked
		{
			return new Vertex( (short)vector.X, (short)vector.Y, (short)vector.Z );
		}
	}

	public long LengthSquared => X * X + Y * Y + Z * Z;
	public double Length => Math.Sqrt( LengthSquared );

	public static Vector3Long operator *( Vector3Long vector, long scalar ) =>
		new( vector.X * scalar, vector.Y * scalar, vector.Z * scalar );

	public static Vector3Long operator /( Vector3Long vector, long scalar ) =>
		new( vector.X / scalar, vector.Y / scalar, vector.Z / scalar );

	public static long Dot( Vector3Long a, Vector3Long b )
	{
		return a.X * b.X + a.Y * b.Y + a.Z * b.Z;
	}

	public static Vector3Long Cross( Vector3Long a, Vector3Long b )
	{
		return new Vector3Long( a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X );
	}
}

public readonly record struct IntPlane( Vector3Long Cross, long Offset )
{
	public Plane Plane
	{
		get
		{
			var invLength = 1d / Cross.Length;

			var normalX = Cross.X * invLength;
			var normalY = Cross.Y * invLength;
			var normalZ = Cross.Z * invLength;

			return new Plane( new Vector3( (float)normalX, (float)normalY, (float)normalZ ), (float)(Offset * invLength) );
		}
	}

	internal Sign GetSide( Vertex point )
	{
		return (Sign)Math.Sign( Dot( point ) );
	}

	public long Dot( Vertex point )
	{
		return Vector3Long.Dot( Cross, (Vector3Int)point ) - Offset;
	}

	internal Vertex? Intersect( Simplex2 line )
	{
		var aDot = Dot( line.A );
		var bDot = Dot( line.B );

		if ( aDot == 0 && bDot == 0 ) return null;

		if ( aDot == 0 ) return line.A;
		if ( bDot == 0 ) return line.B;

		if ( aDot < 0 && bDot < 0 ) return null;
		if ( aDot > 0 && bDot > 0 ) return null;

		if ( aDot > bDot )
		{
			(aDot, bDot) = (bDot, aDot);
			line = line.Reverse;
		}

		var numer = -aDot;
		var denom = bDot - aDot;

		var ab = line.B - line.A;
		var ac = (Vector3Int)((Vector3Long)ab * numer / denom);

		return (Vertex)(line.A + ac);
	}

	public IComparer<Vertex> VertexComparer
	{
		get
		{
			var copy = this;
			return Comparer<Vertex>.Create( ( a, b ) => Math.Sign( copy.Dot( a ) - copy.Dot( b ) ) );
		}
	}
}

internal readonly struct Simplex4 : IEquatable<Simplex4>
{
	public static Simplex4 Create( Vertex a, Vertex b, Vertex c, Vertex d )
	{
		if ( a.Equals( b ) || a.Equals( c ) || a.Equals( d ) || b.Equals( c ) || b.Equals( d ) || c.Equals( d ) )
		{
			throw new ArgumentException( "Vertices must be unique." );
		}

		return new Simplex4( a, b, c, d );
	}

	public static Simplex4? TryCreate( Vertex? a, Vertex? b, Vertex? c, Vertex? d )
	{
		return a is not null && b is not null && c is not null && d is not null
			? TryCreate( a.Value, b.Value, c.Value, d.Value )
			: null;
	}

	public static Simplex4? TryCreate( Vertex a, Vertex b, Vertex c, Vertex d )
	{
		if ( a.Equals( b ) || a.Equals( c ) || a.Equals( d ) || b.Equals( c ) || b.Equals( d ) || c.Equals( d ) )
		{
			return null;
		}

		var abc = new Simplex3( a, b, c );

		if ( abc.GetSide( d ) == Sign.Zero )
		{
			return null;
		}

		return new Simplex4( a, b, c, d );
	}

	public Vertex A { get; }
	public Vertex B { get; }
	public Vertex C { get; }
	public Vertex D { get; }

	public Simplex2 AB => new( A, B );
	public Simplex2 BA => new( B, A );
	public Simplex2 AC => new( A, C );
	public Simplex2 CA => new( C, A );
	public Simplex2 AD => new( A, D );
	public Simplex2 DA => new( D, A );
	public Simplex2 BC => new( B, C );
	public Simplex2 CB => new( C, B );
	public Simplex2 BD => new( B, D );
	public Simplex2 DB => new( D, B );
	public Simplex2 CD => new( C, D );
	public Simplex2 DC => new( D, C );

	public Simplex3 ABC => new( A, B, C );
	public Simplex3 ACD => new( A, C, D );
	public Simplex3 ADB => new( A, D, B );
	public Simplex3 BDC => new( B, D, C );

	public IEnumerable<Simplex3> Faces => [ABC, ACD, ADB, BDC];

	internal Simplex4( Vertex a, Vertex b, Vertex c, Vertex d )
	{
		(a, b, c, d) = (a, b, c, d).Sort();

		var abc = new Simplex3( a, b, c );

		switch ( abc.GetSide( d ) )
		{
			case Sign.Positive:
				abc = abc.Reverse;
				break;

			case Sign.Zero:
				throw new ArgumentException( "Vertices can't be coplanar." );
		}

		if ( abc.GetSide( d ) is Sign.Positive )
		{
			abc = abc.Reverse;
		}

		A = abc.A;
		B = abc.B;
		C = abc.C;
		D = d;
	}

	public Sign Intersects( Vertex point )
	{
		var result = Sign.Negative;

		if ( SimplexExtensions.UpdateMax( ref result, ABC.GetSide( point ) ) ) return result;
		if ( SimplexExtensions.UpdateMax( ref result, ACD.GetSide( point ) ) ) return result;
		if ( SimplexExtensions.UpdateMax( ref result, ADB.GetSide( point ) ) ) return result;
		if ( SimplexExtensions.UpdateMax( ref result, BDC.GetSide( point ) ) ) return result;

		return result;
	}

	public Sign Intersects( Simplex3 triangle )
	{
		// triangle intersects a tetrahedron if:
		// a. at least one point of the triangle is inside
		// b. triangle intersects one or more faces

		var result = Sign.Positive;

		if ( SimplexExtensions.UpdateMin( ref result, Intersects( triangle.A ) ) ) return result;
		if ( SimplexExtensions.UpdateMin( ref result, Intersects( triangle.B ) ) ) return result;
		if ( SimplexExtensions.UpdateMin( ref result, Intersects( triangle.C ) ) ) return result;

		if ( SimplexExtensions.UpdateMin( ref result, ABC.Intersects( triangle ) ) ) return result;
		if ( SimplexExtensions.UpdateMin( ref result, ACD.Intersects( triangle ) ) ) return result;
		if ( SimplexExtensions.UpdateMin( ref result, ADB.Intersects( triangle ) ) ) return result;
		if ( SimplexExtensions.UpdateMin( ref result, BDC.Intersects( triangle ) ) ) return result;

		return result;
	}

	public bool Split( Simplex3 tringle, List<Simplex4> result ) => Split( tringle, result, result );
	public void Split( IntPlane plane, List<Simplex4> result ) => Split( plane, result, result );

	public bool Split( Simplex3 tringle, List<Simplex4>? negative, List<Simplex4>? positive )
	{
		if ( Intersects( tringle ) is not Sign.Negative )
		{
			return false;
		}

		Split( tringle.Plane, negative, positive );
		return true;
	}

	public void Split( IntPlane plane, List<Simplex4>? negative, List<Simplex4>? positive )
	{
		Span<(Vertex Vertex, long Dot)> sorted = stackalloc (Vertex, long)[4];

		sorted[0] = (A, plane.Dot( A ));
		sorted[1] = (B, plane.Dot( B ));
		sorted[2] = (C, plane.Dot( C ));
		sorted[3] = (D, plane.Dot( D ));

		sorted.Sort( static ( a, b ) => Math.Sign( a.Dot - b.Dot ) );

		var negativeCount = 0;
		var positiveCount = 0;

		foreach ( var vertex in sorted )
		{
			if ( vertex.Dot < 0 ) negativeCount++;
			if ( vertex.Dot > 0 ) positiveCount++;
		}

		if ( negativeCount == 0 )
		{
			positive?.Add( this );
			return;
		}

		if ( positiveCount == 0 )
		{
			negative?.Add( this );
			return;
		}

		if ( negativeCount > positiveCount )
		{
			sorted.Reverse();
			(negativeCount, positiveCount) = (positiveCount, negativeCount);
			(positive, negative) = (negative, positive);
		}

		var a = sorted[0].Vertex;
		var b = sorted[1].Vertex;
		var c = sorted[2].Vertex;
		var d = sorted[3].Vertex;

		if ( negativeCount == 1 )
		{
			var ab = plane.Intersect( new Simplex2( a, b ) );
			var ac = plane.Intersect( new Simplex2( a, c ) );
			var ad = plane.Intersect( new Simplex2( a, d ) );

			negative?.TryAdd( TryCreate( ab, ac, ad, a ) );

			positive?.TryAdd( TryCreate( b, c, d, ab ) );
			positive?.TryAdd( TryCreate( ab, ac, ad, d ) );
			positive?.TryAdd( TryCreate( ab, ac, c, d ) );
		}
		else
		{
			Assert.AreEqual( 2, negativeCount );

			var ac = plane.Intersect( new Simplex2( a, c ) );
			var ad = plane.Intersect( new Simplex2( a, d ) );
			var bc = plane.Intersect( new Simplex2( b, c ) );
			var bd = plane.Intersect( new Simplex2( b, d ) );

			negative?.TryAdd( TryCreate( a, b, ac, ad ) );
			negative?.TryAdd( TryCreate( b, bc, bd, ad ) );
			negative?.TryAdd( TryCreate( b, bc, ac, ad ) );

			positive?.TryAdd( TryCreate( c, d, ac, bc ) );
			positive?.TryAdd( TryCreate( d, bc, bd, ad ) );
			positive?.TryAdd( TryCreate( d, bc, ac, ad ) );
		}
	}

	public bool Equals( Simplex4 other )
	{
		return A.Equals( other.A ) && B.Equals( other.B ) && C.Equals( other.C ) && D.Equals( other.D );
	}

	public override bool Equals( object? obj )
	{
		return obj is Simplex4 other && Equals( other );
	}

	public override int GetHashCode()
	{
		return HashCode.Combine( A, B, C, D );
	}
}
