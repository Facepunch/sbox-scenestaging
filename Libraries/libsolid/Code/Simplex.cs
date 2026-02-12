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

	public Vertex MidPoint => (Vertex)((A + B) / 2);

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

	public Plane Plane
	{
		get
		{
			var normal = Vector3.Cross( B - A, C - A ).Normal;
			return new( normal, normal.Dot( (Vector3Int)A ) );
		}
	}

	public Vertex A { get; }
	public Vertex B { get; }
	public Vertex C { get; }

	public Simplex2 AB => new( A, B );
	public Simplex2 BC => new( B, C );
	public Simplex2 CA => new( C, A );

	public Simplex2 AChord => new( A, BC.MidPoint );
	public Simplex2 BChord => new( B, CA.MidPoint );
	public Simplex2 CChord => new( C, AB.MidPoint );

	public Simplex3 Reverse => new( A, C, B );

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
		var plane = Plane;
		var otherPlane = other.Plane;

		if ( SimplexExtensions.UpdateMin( ref result, Intersects( other.AB, plane ) ) ) return result;
		if ( SimplexExtensions.UpdateMin( ref result, Intersects( other.BC, plane ) ) ) return result;
		if ( SimplexExtensions.UpdateMin( ref result, Intersects( other.CA, plane ) ) ) return result;

		if ( SimplexExtensions.UpdateMin( ref result, other.Intersects( AB, otherPlane ) ) ) return result;
		if ( SimplexExtensions.UpdateMin( ref result, other.Intersects( BC, otherPlane ) ) ) return result;
		if ( SimplexExtensions.UpdateMin( ref result, other.Intersects( CA, otherPlane ) ) ) return result;

		return result;
	}

	public Sign Intersects( Simplex2 line ) => Intersects( line, Plane );

	private Sign Intersects( Simplex2 line, Plane plane )
	{
		var sideA = plane.GetSide( line.A );
		var sideB = plane.GetSide( line.B );

		if ( sideA == Sign.Zero || sideB == Sign.Zero ) return Sign.Zero;

		if ( sideA == sideB ) return Sign.Positive;

		if ( sideA == Sign.Positive )
		{
			line = line.Reverse;
		}

		if ( A == line.A || A == line.B ) return Sign.Zero;
		if ( B == line.A || B == line.B ) return Sign.Zero;
		if ( C == line.A || C == line.B ) return Sign.Zero;

		var result = Sign.Negative;

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
		
		if ( new Simplex3( a, b, c ).GetSide( d ) == Sign.Zero )
		{
			return null;
		}

		if ( new Simplex3( a, c, d ).GetSide( b ) == Sign.Zero )
		{
			return null;
		}

		if ( new Simplex3( a, b, d ).GetSide( c ) == Sign.Zero )
		{
			return null;
		}

		if ( new Simplex3( b, c, d ).GetSide( a ) == Sign.Zero )
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
		// a. center of the triangle is inside
		// b. triangle intersects one or more faces

		var result = Sign.Positive;

		var center = (triangle.A + triangle.B + triangle.C) / 3;

		if ( SimplexExtensions.UpdateMin( ref result, Intersects( (Vertex)center ) ) ) return result;

		if ( SimplexExtensions.UpdateMin( ref result, ABC.Intersects( triangle ) ) ) return result;
		if ( SimplexExtensions.UpdateMin( ref result, ACD.Intersects( triangle ) ) ) return result;
		if ( SimplexExtensions.UpdateMin( ref result, ADB.Intersects( triangle ) ) ) return result;
		if ( SimplexExtensions.UpdateMin( ref result, BDC.Intersects( triangle ) ) ) return result;

		return result;
	}

	public bool Split( Simplex3 tringle, List<Simplex4> result ) => Split( tringle, result, result );
	public void Split( Plane plane, List<Simplex4> result ) => Split( plane, result, result );

	public bool Split( Simplex3 tringle, List<Simplex4>? negative, List<Simplex4>? positive )
	{
		if ( Intersects( tringle ) is not Sign.Negative )
		{
			return false;
		}

		Split( tringle.Plane, negative, positive );
		return true;
	}

	public void Split( Plane plane, List<Simplex4>? negative, List<Simplex4>? positive )
	{
		Span<(Vertex Vertex, float Distance)> sorted = stackalloc (Vertex, float)[4];

		sorted[0] = (A, plane.GetDistance( A ));
		sorted[1] = (B, plane.GetDistance( B ));
		sorted[2] = (C, plane.GetDistance( C ));
		sorted[3] = (D, plane.GetDistance( D ));

		sorted.Sort( static ( a, b ) => Math.Sign( a.Distance - b.Distance ) );

		var negativeCount = 0;
		var positiveCount = 0;

		foreach ( var vertex in sorted )
		{
			if ( vertex.Distance <= SimplexExtensions.DistanceEpsilon ) negativeCount++;
			if ( vertex.Distance >= SimplexExtensions.DistanceEpsilon ) positiveCount++;
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
