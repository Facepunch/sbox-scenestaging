using System.Collections.Generic;

namespace Sandbox.Solids;

public sealed class MeshWriter
{
	public readonly struct MeshVertex
	{
		[VertexLayout.Position]
		public readonly Vector3 Position;

		[VertexLayout.Normal]
		public readonly Vector3 Normal;

		[VertexLayout.Tangent]
		public readonly Vector4 Tangent;

		public MeshVertex( Vector3 position, Vector3 normal, Vector4 tangent )
		{
			Position = position;
			Normal = normal;
			Tangent = tangent;
		}
	}

	private readonly List<MeshVertex> _vertices = new();
	private readonly List<int> _indices = new();

	public bool IsEmpty => _indices.Count == 0;

	public void Clear()
	{
		_vertices.Clear();
		_indices.Clear();
	}

	public void Write( Solid solid, int shift )
	{
		foreach ( var cell in solid.Cells )
		{
			Write( cell, shift );
		}
	}

	internal void Write( Cell cell, int shift )
	{
		Write( cell.Shape.ABC, cell.Material, shift );
		Write( cell.Shape.ACD, cell.Material, shift );
		Write( cell.Shape.ADB, cell.Material, shift );
		Write( cell.Shape.BDC, cell.Material, shift );
	}

	internal void Write( Simplex3 triangle, SolidMaterial material, int shift )
	{
		var normal = triangle.Plane.Normal;
		var tangent = new Vector4( (triangle.B - triangle.A).Normal, 1f );

		var index = _vertices.Count;

		_vertices.Add( new MeshVertex( triangle.A.FromFixed( shift ), normal, tangent ) );
		_vertices.Add( new MeshVertex( triangle.B.FromFixed( shift ), normal, tangent ) );
		_vertices.Add( new MeshVertex( triangle.C.FromFixed( shift ), normal, tangent ) );

		Write( index, index + 1, index + 2 );
	}

	private void Write( int a, int b, int c )
	{
		_indices.Add( a );
		_indices.Add( b );
		_indices.Add( c );
	}

	public void CopyTo( Mesh mesh )
	{
		if ( !mesh.HasIndexBuffer )
		{
			mesh.CreateIndexBuffer( _indices.Count, _indices );
			mesh.CreateVertexBuffer( _vertices.Count, _vertices );
		}
		else
		{
			mesh.SetIndexBufferSize( _indices.Count );
			mesh.SetIndexBufferData( _indices );

			mesh.SetVertexBufferSize( _vertices.Count );
			mesh.SetVertexBufferData( _vertices );
		}

		mesh.SetIndexRange( 0, _indices.Count );
	}
}
