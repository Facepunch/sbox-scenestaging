using System;
using System.Collections.Generic;
using System.Linq;

namespace Sandbox.Solids;

partial class Solid
{
	public Solid Add( Solid other )
	{
		throw new NotImplementedException();
	}

	public Solid? Subtract( Solid other )
	{
		var cells = new List<Cell>( _cells );

		Split( cells, other.Cells.Select( x => x.Shape ) );
		RemoveOverlapping( cells, other.Cells.Select( x => x.Shape ) );

		return cells.Count == 0 ? null : new Solid( cells );
	}

	private static void Split( List<Cell> cells, IEnumerable<Simplex4> shapes )
	{
		Split( cells, GetSurfaceTriangles( shapes ) );
	}

	private static IEnumerable<Simplex3> GetSurfaceTriangles( IEnumerable<Simplex4> shapes )
	{
		var uniqueTriangles = new HashSet<Simplex3>();

		foreach ( var shape in shapes )
		{
			uniqueTriangles.Add( shape.ABC );
			uniqueTriangles.Add( shape.ACD );
			uniqueTriangles.Add( shape.ADB );
			uniqueTriangles.Add( shape.BDC );
		}

		foreach ( var triangle in uniqueTriangles )
		{
			if ( !uniqueTriangles.Contains( triangle.Reverse ) )
			{
				yield return triangle;
			}
		}
	}

	private static void Split( List<Cell> cells, IEnumerable<Simplex3> triangles )
	{
		var shapes = new List<Simplex4>();

		foreach ( var triangle in triangles )
		{
			for ( var i = cells.Count - 1; i >= 0; i-- )
			{
				var cell = cells[i];

				shapes.Clear();

				if ( !cell.Shape.Split( triangle, shapes ) ) continue;

				cells.RemoveAt( i );

				foreach ( var shape in shapes )
				{
					cells.Add( cell with { Shape = shape } );
				}
			}
		}
	}

	private readonly record struct Simplex4Planes( Plane A, Plane B, Plane C, Plane D )
	{
		public Simplex4Planes( Simplex4 tetra )
			: this( tetra.ABC.Plane, tetra.ACD.Plane, tetra.ADB.Plane, tetra.BDC.Plane )
		{

		}

		public Sign Intersects( Vertex point )
		{
			var result = Sign.Negative;

			if ( SimplexExtensions.UpdateMax( ref result, A.GetSide( point ) ) ) return result;
			if ( SimplexExtensions.UpdateMax( ref result, B.GetSide( point ) ) ) return result;
			if ( SimplexExtensions.UpdateMax( ref result, C.GetSide( point ) ) ) return result;
			if ( SimplexExtensions.UpdateMax( ref result, D.GetSide( point ) ) ) return result;

			return result;
		}
	}

	private static void RemoveOverlapping( List<Cell> cells, IEnumerable<Simplex4> mask )
	{
		var maskPlanes = mask
			.Select( x => new Simplex4Planes( x ) )
			.ToArray();

		cells.RemoveAll( x =>
		{
			var center = (Vertex)((x.Shape.A + x.Shape.B + x.Shape.C + x.Shape.D) / 4);

			foreach ( var planes in maskPlanes )
			{
				if ( planes.Intersects( center ) <= Sign.Zero )
				{
					return true;
				}
			}

			return false;
		} );
	}

	public Solid Split( Vertex a, Vertex b, Vertex c ) =>
		Split( Simplex3.Create( a, b, c ) );

	internal Solid Split( Simplex3 triangle )
	{
		var plane = triangle.Plane;

		var minSide = Sign.Positive;
		var maxSide = Sign.Negative;

		foreach ( var vertex in _vertices )
		{
			var side = plane.GetSide( vertex );

			minSide = SimplexExtensions.Min( minSide, side );
			maxSide = SimplexExtensions.Max( maxSide, side );
		}

		if ( minSide >= Sign.Zero || maxSide <= Sign.Zero ) return this;

		var cells = new List<Cell>();
		var shapes = new List<Simplex4>();

		foreach ( var cell in _cells )
		{
			shapes.Clear();

			if ( !cell.Shape.Split( triangle, shapes ) )
			{
				cells.Add( cell );
				continue;
			}

			foreach ( var shape in shapes )
			{
				cells.Add( cell with { Shape = shape } );
			}
		}

		return new Solid( cells );
	}
}

internal static class SimplexExtensions
{
	public static bool UpdateMin( ref Sign best, Sign result )
	{
		best = Min( best, result );
		return best == Sign.Negative;
	}

	public static bool UpdateMin( ref Sign best, ref int zeroCount, Sign result )
	{
		best = Min( best, result );

		if ( result == Sign.Zero )
		{
			zeroCount++;

			if ( zeroCount == 2 )
			{
				best = Sign.Negative;
			}
		}

		return best == Sign.Negative;
	}

	public static bool UpdateMax( ref Sign best, Sign result )
	{
		best = Max( best, result );
		return best == Sign.Positive;
	}

	public static Sign Min( Sign a, Sign b )
	{
		return a <= b ? a : b;
	}

	public static Sign Max( Sign a, Sign b )
	{
		return a >= b ? a : b;
	}

	public static (T, T, T, T) Sort<T>( this (T, T, T, T) tuple )
		where T : unmanaged, IComparable<T>
	{
		Span<T> span = stackalloc T[4];

		span[0] = tuple.Item1;
		span[1] = tuple.Item2;
		span[2] = tuple.Item3;
		span[3] = tuple.Item4;

		span.Sort();

		return (span[0], span[1], span[2], span[3]);
	}

	public static void TryAdd( this List<Simplex4> list, Simplex4? tetra )
	{
		if ( tetra.HasValue )
		{
			list.Add( tetra.Value );
		}
	}

	public const float DistanceEpsilon = 1.5f;

	public static Sign GetSide( this Plane plane, Vertex point )
	{
		var dist = plane.GetDistance( point );

		return dist switch
		{
			<= -DistanceEpsilon => Sign.Negative,
			>= DistanceEpsilon => Sign.Positive,
			_ => Sign.Zero
		};
	}

	public static Vertex? Intersect( this Plane plane, Simplex2 line )
	{
		var aDot = plane.GetDistance( line.A );
		var bDot = plane.GetDistance( line.B );

		if ( aDot.AlmostEqual( 0f, DistanceEpsilon ) && bDot.AlmostEqual( 0f, DistanceEpsilon ) ) return null;

		if ( aDot.AlmostEqual( 0f, DistanceEpsilon ) ) return line.A;
		if ( bDot.AlmostEqual( 0f, DistanceEpsilon ) ) return line.B;

		if ( aDot < -DistanceEpsilon && bDot < -DistanceEpsilon ) return null;
		if ( aDot > DistanceEpsilon && bDot > DistanceEpsilon ) return null;

		if ( aDot > bDot )
		{
			(aDot, bDot) = (bDot, aDot);
			line = line.Reverse;
		}

		var numer = -aDot;
		var denom = bDot - aDot;

		var ab = line.B - line.A;
		var ac = ab * numer / denom;

		return (Vertex)(line.A + new Vector3Int( (int)MathF.Round( ac.x ), (int)MathF.Round( ac.y ), (int)MathF.Round( ac.z ) ));
	}
}

internal enum Sign
{
	Negative = -1,
	Zero = 0,
	Positive = 1
}
