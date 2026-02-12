using Sandbox.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sandbox.Solids;

public readonly record struct Vertex( short X, short Y, short Z ) : IComparable<Vertex>
{
	public static Vertex MinValue { get; } = new( short.MinValue, short.MinValue, short.MinValue );
	public static Vertex MaxValue { get; } = new( short.MaxValue, short.MaxValue, short.MaxValue );

	public static explicit operator Vertex( Vector3Int position )
	{
		checked
		{
			return new( (short)position.x, (short)position.y, (short)position.z );
		}
	}

	public static implicit operator Vector3Int( Vertex vertex ) =>
		new( vertex.X, vertex.Y, vertex.Z );

	public static Vector3Int operator +( Vertex a, Vertex b ) => new( a.X + b.X, a.Y + b.Y, a.Z + b.Z );
	public static Vector3Int operator -( Vertex a, Vertex b ) => new( a.X - b.X, a.Y - b.Y, a.Z - b.Z );

	public static Vertex Min( Vertex a, Vertex b )
	{
		return new Vertex( Math.Min( a.X, b.X ), Math.Min( a.Y, b.Y ), Math.Min( a.Z, b.Z ) );
	}

	public static Vertex Max( Vertex a, Vertex b )
	{
		return new Vertex( Math.Max( a.X, b.X ), Math.Max( a.Y, b.Y ), Math.Max( a.Z, b.Z ) );
	}

	public Vector3 FromFixed( int shift )
	{
		var scale = 1f / (1 << shift);

		return new Vector3( X * scale, Y * scale, Z * scale );
	}

	public int CompareTo( Vertex other )
	{
		var xComparison = X.CompareTo( other.X );
		if ( xComparison != 0 ) return xComparison;

		var yComparison = Y.CompareTo( other.Y );
		if ( yComparison != 0 ) return yComparison;

		return Z.CompareTo( other.Z );
	}
}

public sealed partial class Solid
{
	private readonly Vertex[] _vertices;
	private readonly Cell[] _cells;

	public int VertexCount => _vertices.Length;
	public int CellCount => _cells.Length;

	public Bounds Bounds { get; }

	internal IReadOnlyList<Cell> Cells => _cells;

	internal Solid( params IEnumerable<Cell> cells )
	{
		var uniqueVertices = new HashSet<Vertex>();

		_cells = cells.ToArray();

		foreach ( var cell in _cells )
		{
			uniqueVertices.Add( cell.Shape.A );
			uniqueVertices.Add( cell.Shape.B );
			uniqueVertices.Add( cell.Shape.C );
			uniqueVertices.Add( cell.Shape.D );
		}

		_vertices = uniqueVertices.ToArray();

		Bounds = FindBounds( _vertices );

		AssertIsValid();
	}

	private void AssertIsValid()
	{
		Assert.True( _vertices.Length >= 4, "Expected at least 4 vertices." );
		Assert.True( _cells.Length >= 1, "Expected at least 1 cell." );

		// TODO: assert cells don't overlap
	}

	public void DrawGizmos( int shift )
	{
		foreach ( var triangle in GetSurfaceTriangles( _cells.Select( x => x.Shape ) ) )
		{
			var a = triangle.A.FromFixed( shift );
			var b = triangle.B.FromFixed( shift );
			var c = triangle.C.FromFixed( shift );

			Gizmo.Draw.Line( a, b );
			Gizmo.Draw.Line( a, c );
			Gizmo.Draw.Line( b, c );
		}

		//foreach ( var cell in _cells )
		//{
		//	var a = cell.Shape.A.FromFixed( shift );
		//	var b = cell.Shape.B.FromFixed( shift );
		//	var c = cell.Shape.C.FromFixed( shift );
		//	var d = cell.Shape.D.FromFixed( shift );

		//	Gizmo.Draw.Line( a, b );
		//	Gizmo.Draw.Line( a, c );
		//	Gizmo.Draw.Line( a, d );
		//	Gizmo.Draw.Line( b, c );
		//	Gizmo.Draw.Line( b, d );
		//	Gizmo.Draw.Line( c, d );
		//}
	}

	public static Solid Tetrahedron( Vertex a, Vertex b, Vertex c, Vertex d, SolidMaterial material )
	{
		return new Solid( new Cell( Simplex4.Create( a, b, c, d ), material ) );
	}

	public static Solid Box( Vertex min, Vertex max, SolidMaterial material )
	{
		return Cuboid( min,
			new Vertex( (short)(max.X - min.X), 0, 0 ),
			new Vertex( 0, (short)(max.Y - min.Y), 0 ),
			new Vertex( 0, 0, (short)(max.Z - min.Z) ),
			material);
	}

	public static Solid Cuboid( Vertex origin, Vertex unitX, Vertex unitY, Vertex unitZ, SolidMaterial material )
	{
		var v0 = origin;
		var v1 = (Vertex)(origin + unitX);
		var v2 = (Vertex)(origin + unitY);
		var v3 = (Vertex)(origin + unitX + unitY);
		var v4 = (Vertex)(origin + unitZ);
		var v5 = (Vertex)(origin + unitX + unitZ);
		var v6 = (Vertex)(origin + unitY + unitZ);
		var v7 = (Vertex)(origin + unitX + unitY + unitZ);

		var cells = new Cell[]
		{
			new( Simplex4.Create( v1, v2, v4, v0 ), material ),
			new( Simplex4.Create( v1, v7, v2, v3 ), material ),
			new( Simplex4.Create( v1, v4, v7, v5 ), material ),
			new( Simplex4.Create( v2, v7, v4, v6 ), material ),
			new( Simplex4.Create( v1, v2, v7, v4 ), material )
		};

		return new Solid( cells );
	}

	private static Bounds FindBounds( IEnumerable<Vertex> vertices )
	{
		var min = Vertex.MaxValue;
		var max = Vertex.MinValue;

		foreach ( var vertex in vertices )
		{
			min = Vertex.Min( min, vertex );
			max = Vertex.Max( max, vertex );
		}

		return new Bounds( min, max );
	}
}

internal readonly record struct Cell( Simplex4 Shape, SolidMaterial Material );

//internal record struct Face<T>( Simplex3 Shape, SolidMaterial? Material );
