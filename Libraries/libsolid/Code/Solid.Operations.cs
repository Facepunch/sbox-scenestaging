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
		var splitCount = 0;

		foreach ( var triangle in triangles )
		{
			var anySplits = false;

			for ( var i = cells.Count - 1; i >= 0; i-- )
			{
				var cell = cells[i];

				shapes.Clear();

				if ( !cell.Shape.Split( triangle, shapes ) ) continue;

				anySplits = true;

				cells.RemoveAt( i );

				foreach ( var shape in shapes )
				{
					cells.Add( cell with { Shape = shape } );
				}
			}

			if ( anySplits ) splitCount++;

			if ( splitCount >= 2 ) break;
		}
	}

	private readonly record struct Simplex4Planes( IntPlane A, IntPlane B, IntPlane C, IntPlane D )
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
}

internal ref struct SpanList<T>( Span<T> span )
{
	private readonly Span<T> _span = span;
	private int _count;

	public int Count => _count;
	public int Capacity => _span.Length;

	public T this[ int index ] => _span[index];

	public void Add( T value )
	{
		if ( Count >= Capacity )
		{
			throw new InvalidOperationException( "Exceeded capacity." );
		}

		_span[_count++] = value;
	}

	public Span<T> AsSpan() => _span[.._count];
}

internal enum Sign
{
	Negative = -1,
	Zero = 0,
	Positive = 1
}
