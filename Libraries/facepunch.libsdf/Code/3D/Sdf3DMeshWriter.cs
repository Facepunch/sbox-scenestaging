using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Sandbox.Sdf;

internal partial class Sdf3DMeshWriter : Pooled<Sdf3DMeshWriter>, IMeshWriter
{
	private ConcurrentQueue<Triangle> Triangles { get; } = new ConcurrentQueue<Triangle>();
	private Dictionary<(VertexKey Key, UvPlane Plane), int> VertexMap { get; } = new();

	public List<Vertex> Vertices { get; } = new List<Vertex>();
	public List<Vector3> VertexPositions { get; } = new List<Vector3>();
	public List<int> Indices { get; } = new List<int>();

	public bool IsEmpty => Indices.Count == 0;

	public byte[] Samples { get; set; }

	public override void Reset()
	{
		Triangles.Clear();
		VertexMap.Clear();

		Vertices.Clear();
		VertexPositions.Clear();
		Indices.Clear();
	}

	private void WriteSlice( in Sdf3DArrayData data, Sdf3DVolume volume, int z )
	{
		var quality = volume.Quality;
		var size = quality.ChunkResolution;

		for ( var y = 0; y < size; ++y )
			for ( var x = 0; x < size; ++x )
				AddTriangles( in data, x, y, z );
	}

	public async Task WriteAsync( Sdf3DArrayData data, Sdf3DVolume volume )
	{
		Triangles.Clear();
		VertexMap.Clear();

		var baseIndex = Vertices.Count;

		var quality = volume.Quality;
		var size = quality.ChunkResolution;

		var tasks = new List<Task>();

		for ( var z = 0; z < size; ++z )
		{
			var zCopy = z;

			tasks.Add( GameTask.RunInThreadAsync( () =>
			{
				WriteSlice( data, volume, zCopy );
			} ) );
		}

		await GameTask.WhenAll( tasks );

		await GameTask.WorkerThread();

		var unitSize = volume.Quality.UnitSize;

		foreach ( var triangle in Triangles )
		{
			var pos0 = GetPosition( data, triangle.V0 );
			var pos1 = GetPosition( data, triangle.V1 );
			var pos2 = GetPosition( data, triangle.V2 );

			var uvPlane = GetUvPlane( pos0, pos1, pos2 );

			Indices.Add( AddVertex( data, triangle.V0, uvPlane, pos0, unitSize ) );
			Indices.Add( AddVertex( data, triangle.V1, uvPlane, pos1, unitSize ) );
			Indices.Add( AddVertex( data, triangle.V2, uvPlane, pos2, unitSize ) );
		}

		for ( var i = baseIndex; i < Vertices.Count; ++i )
		{
			var vertex = Vertices[i];

			Vertices[i] = vertex with { Normal = vertex.Normal.Normal };
		}
	}

	private static UvPlane GetUvPlane( Vector3 pos0, Vector3 pos1, Vector3 pos2 )
	{
		var cross = Vector3.Cross( pos1 - pos0, pos2 - pos0 );

		var absX = MathF.Abs( cross.x );
		var absY = MathF.Abs( cross.y );
		var absZ = MathF.Abs( cross.z );

		return absX >= absY && absX >= absZ ? cross.x > 0f ? UvPlane.PosX : UvPlane.NegX
			: absY >= absZ ? cross.y > 0f ? UvPlane.PosY : UvPlane.NegY
			: cross.z > 0f ? UvPlane.PosZ : UvPlane.NegZ;
	}

	private static (Vector3 U, Vector3 V) GetTangents( UvPlane plane )
	{
		return plane switch
		{
			UvPlane.PosX => (new Vector3( 0f, 0f, -1f ), new Vector3( 0f, 1f, 0f )),
			UvPlane.NegX => (new Vector3( 0f, 0f, 1f ), new Vector3( 0f, 1f, 0f )),

			UvPlane.PosY => (new Vector3( 1f, 0f, 0f ), new Vector3( 0f, 0f, 1f )),
			UvPlane.NegY => (new Vector3( -1f, 0f, 0f ), new Vector3( 0f, 0f, 1f )),

			UvPlane.PosZ => (new Vector3( 1f, 0f, 0f ), new Vector3( 0f, 1f, 0f )),
			UvPlane.NegZ => (new Vector3( -1f, 0f, 0f ), new Vector3( 0f, 1f, 0f )),

			_ => throw new NotImplementedException()
		};
	}

	public void ApplyTo( Mesh mesh )
	{
		ThreadSafe.AssertIsMainThread();

		if ( mesh == null )
		{
			return;
		}

		if ( mesh.HasVertexBuffer )
		{
			if ( Indices.Count > 0 )
			{
				if ( mesh.IndexCount < Indices.Count )
				{
					mesh.SetIndexBufferSize( Indices.Count );
				}

				if ( mesh.VertexCount < Vertices.Count )
				{
					mesh.SetVertexBufferSize( Vertices.Count );
				}

				mesh.SetIndexBufferData( Indices );
				mesh.SetVertexBufferData( Vertices );
			}

			mesh.SetIndexRange( 0, Indices.Count );
		}
		else if ( Indices.Count > 0 )
		{
			mesh.CreateVertexBuffer( Vertices.Count, Vertex.Layout, Vertices );
			mesh.CreateIndexBuffer( Indices.Count, Indices );
		}
	}

	private static Vector3 GetPosition( in Sdf3DArrayData data, VertexKey key )
	{
		switch ( key.Vertex )
		{
			case NormalizedVertex.A:
				{
					return new Vector3( key.X, key.Y, key.Z );
				}

			case NormalizedVertex.AB:
				{
					var a = data[key.X, key.Y, key.Z] - 127.5f;
					var b = data[key.X + 1, key.Y, key.Z] - 127.5f;
					var t = a / (a - b);

					return new Vector3( key.X + t, key.Y, key.Z );
				}

			case NormalizedVertex.AC:
				{
					var a = data[key.X, key.Y, key.Z] - 127.5f;
					var c = data[key.X, key.Y + 1, key.Z] - 127.5f;
					var t = a / (a - c);

					return new Vector3( key.X, key.Y + t, key.Z );
				}

			case NormalizedVertex.AE:
				{
					var a = data[key.X, key.Y, key.Z] - 127.5f;
					var e = data[key.X, key.Y, key.Z + 1] - 127.5f;
					var t = a / (a - e);

					return new Vector3( key.X, key.Y, key.Z + t );
				}

			default:
				throw new NotImplementedException();
		}
	}

	private static Vertex GetVertex( in Sdf3DArrayData data, VertexKey key, UvPlane plane, Vector3 pos )
	{
		float xNeg, xPos, yNeg, yPos, zNeg, zPos;

		switch ( key.Vertex )
		{
			case NormalizedVertex.A:
			{
				xNeg = data[key.X - 1, key.Y, key.Z];
				xPos = data[key.X + 1, key.Y, key.Z];
				yNeg = data[key.X, key.Y - 1, key.Z];
				yPos = data[key.X, key.Y + 1, key.Z];
				zNeg = data[key.X, key.Y, key.Z - 1];
				zPos = data[key.X, key.Y, key.Z + 1];
				break;
			}

			case NormalizedVertex.AB:
			{
				xNeg = data[pos.x - 1, key.Y, key.Z];
				xPos = data[pos.x + 1, key.Y, key.Z];
				yNeg = data[pos.x, key.Y - 1, key.Z];
				yPos = data[pos.x, key.Y + 1, key.Z];
				zNeg = data[pos.x, key.Y, key.Z - 1];
				zPos = data[pos.x, key.Y, key.Z + 1];
				break;
			}

			case NormalizedVertex.AC:
			{
				xNeg = data[key.X - 1, pos.y, key.Z];
				xPos = data[key.X + 1, pos.y, key.Z];
				yNeg = data[key.X, pos.y - 1, key.Z];
				yPos = data[key.X, pos.y + 1, key.Z];
				zNeg = data[key.X, pos.y, key.Z - 1];
				zPos = data[key.X, pos.y, key.Z + 1];
				break;
			}

			case NormalizedVertex.AE:
			{
				xNeg = data[key.X - 1, key.Y, pos.z];
				xPos = data[key.X + 1, key.Y, pos.z];
				yNeg = data[key.X, key.Y - 1, pos.z];
				yPos = data[key.X, key.Y + 1, pos.z];
				zNeg = data[key.X, key.Y, pos.z - 1];
				zPos = data[key.X, key.Y, pos.z + 1];
				break;
			}

			default:
				throw new NotImplementedException();
		}

		var normal = new Vector3( xPos - xNeg, yPos - yNeg, zPos - zNeg ).Normal;
		var basisTangents = GetTangents( plane );

		var u = Vector3.Dot( basisTangents.U, pos );
		var v = Vector3.Dot( basisTangents.V, pos );

		var tangent = Vector3.Cross( basisTangents.V, normal );
		var binormal = Vector3.Cross( tangent, normal );

		return new Vertex( pos, normal,
			new Vector4( tangent, MathF.Sign( Vector3.Dot( basisTangents.V, binormal ) ) ),
			new Vector2( u, v ) );
	}

	partial void AddTriangles( in Sdf3DArrayData data, int x, int y, int z );

	private void AddTriangle( int x, int y, int z, CubeVertex v0, CubeVertex v1, CubeVertex v2 )
	{
		Triangles.Enqueue( new Triangle( x, y, z, v0, v1, v2 ) );
	}

	private int AddVertex( in Sdf3DArrayData data, VertexKey key, UvPlane plane, Vector3 pos, float unitSize )
	{
		if ( VertexMap.TryGetValue( (key, plane), out var index ) )
		{
			return index;
		}

		index = Vertices.Count;

		var vertex = GetVertex( in data, key, plane, pos );

		vertex = vertex with { Position = vertex.Position * unitSize };

		Vertices.Add( vertex );
		VertexPositions.Add( vertex.Position );

		VertexMap.Add( (key, plane), index );

		return index;
	}
}
