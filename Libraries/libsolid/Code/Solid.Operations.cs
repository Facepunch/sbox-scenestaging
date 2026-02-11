using System;
using System.Collections.Generic;

namespace Sandbox.Solids;

partial class Solid
{
	public Solid Add( Solid other )
	{
		throw new NotImplementedException();
	}

	public Solid Subtract( Solid other )
	{
		throw new NotImplementedException();
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

		var newCells = new List<Cell>();
		var shapes = new List<Simplex4>();

		foreach ( var cell in _cells )
		{
			shapes.Clear();

			if ( !cell.Shape.Split( triangle, shapes ) )
			{
				newCells.Add( cell );
				continue;
			}

			foreach ( var shape in shapes )
			{
				newCells.Add( cell with { Shape = shape } );
			}
		}

		return new Solid( newCells );
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
