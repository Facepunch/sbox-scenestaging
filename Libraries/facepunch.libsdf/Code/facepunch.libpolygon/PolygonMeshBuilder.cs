using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Sdf;

namespace Sandbox.Polygons;

/// <summary>
/// Helper class for building 3D meshes based on a 2D polygon. Supports
/// concave polygons with holes, although edges must not intersect.
/// </summary>
public partial class PolygonMeshBuilder : Pooled<PolygonMeshBuilder>
{
	public record struct Vertex( Vector3 Position, Vector3 Normal, Vector4 Tangent )
	{
		public static VertexAttribute[] Layout { get; } = new[]
		{
			new VertexAttribute( VertexAttributeType.Position, VertexAttributeFormat.Float32 ),
			new VertexAttribute( VertexAttributeType.Normal, VertexAttributeFormat.Float32 ),
			new VertexAttribute( VertexAttributeType.Tangent, VertexAttributeFormat.Float32, 4 )
		};
	}

	private int _nextEdgeIndex;
	private Edge[] _allEdges = new Edge[64];
	private readonly HashSet<int> _activeEdges = new ();

	private readonly List<Vertex> _vertices = new ();
	private readonly List<int> _indices = new ();

	private float _prevDistance;
	private float _nextDistance;

	private float _invDistance;

	private float _prevHeight;
	private float _nextHeight;

	private float _prevAngle;
	private float _nextAngle;

	private float _minSmoothNormalDot;

	private bool _validated;

	/// <summary>
	/// Number of edges that will be affected by calls to methods like <see cref="Bevel"/>, <see cref="Round"/>, and <see cref="Close"/>.
	/// </summary>
	public int ActiveEdgeCount => _activeEdges.Count;

	/// <summary>
	/// If true, no active edges remain because the mesh is fully closed.
	/// </summary>
	public bool IsClosed => _activeEdges.Count == 0;

	/// <summary>
	/// Corners of the original polygon with an interior or exterior
	/// angle less than this (in radians) will have smooth normals.
	/// </summary>
	public float MaxSmoothAngle { get; set; } = 0f;

	/// <summary>
	/// If true, don't bother generating normals / tangents.
	/// </summary>
	public bool SkipNormals { get; set; }

	/// <summary>
	/// Positions of each vertex in the generated mesh.
	/// </summary>
	public IEnumerable<Vector3> Positions => _vertices.Select( x => x.Position );

	/// <summary>
	/// Normals of each vertex in the generated mesh.
	/// </summary>
	public IEnumerable<Vector3> Normals => _vertices.Select( x => x.Normal );

	/// <summary>
	/// U-tangents, and the signs of the V-tangents, of each vertex in the generated mesh.
	/// </summary>
	public IEnumerable<Vector4> Tangents => _vertices.Select( x => x.Tangent );

	/// <summary>
	/// Positions, normals, and tangents of each vertex.
	/// </summary>
	public List<Vertex> Vertices => _vertices;

	/// <summary>
	/// Indices of vertices describing the triangulation of the generated mesh.
	/// </summary>
	public List<int> Indices => _indices;

	/// <summary>
	/// Clear all geometry from this builder.
	/// </summary>
	public PolygonMeshBuilder Clear()
	{
		_nextEdgeIndex = 0;
		_activeEdges.Clear();

		_vertices.Clear();
		_indices.Clear();

		_prevDistance = 0f;
		_nextDistance = 0f;

		_invDistance = 0f;

		_prevHeight = 0f;
		_nextHeight = 0f;

		_prevAngle = 0f;
		_nextAngle = 0f;

		_minSmoothNormalDot = 0f;

		_validated = true;

		return this;
	}

	/// <summary>
	/// Reset this builder to be like a new instance.
	/// </summary>
	public override void Reset()
	{
		Clear();

		MaxSmoothAngle = 0f;
		SkipNormals = false;
	}

	private static int NextPowerOfTwo( int value )
	{
		var po2 = 1;
		while ( po2 < value )
		{
			po2 <<= 1;
		}

		return po2;
	}

	private void EnsureCapacity( int toAdd )
	{
		if ( _nextEdgeIndex + toAdd > _allEdges.Length )
		{
			Array.Resize( ref _allEdges, NextPowerOfTwo( _nextEdgeIndex + toAdd ) );
		}
	}

	private int AddEdge( Vector2 origin, Vector2 tangent, float distance, int? twinOffset = null )
	{
		var edge = new Edge( _nextEdgeIndex, origin, tangent, distance, twinOffset != null ? _nextEdgeIndex + twinOffset.Value : -1 );
		_allEdges[edge.Index] = edge;
		++_nextEdgeIndex;
		return edge.Index;
	}

	private void Invalidate()
	{
		_validated = false;
	}

	/// <summary>
	/// Add a set of active edges forming a loop. Clockwise loops will be a solid polygon, and count-clockwise
	/// will form a hole. Holes must be inside of solid polygons, otherwise the mesh can't be closed correctly.
	/// </summary>
	/// <param name="vertices">List of vertices to read a range from.</param>
	/// <param name="offset">Index of the first vertex in the loop.</param>
	/// <param name="count">Number of vertices in the loop.</param>
	/// <param name="reverse">If true, reverse the order of the vertices in the loop.</param>
	public PolygonMeshBuilder AddEdgeLoop( IReadOnlyList<Vector2> vertices, int offset, int count, bool reverse = false )
	{
		return AddEdgeLoop( vertices, offset, count, Vector2.Zero, Vector2.One, reverse );
	}

	public PolygonMeshBuilder AddEdgeLoop( IReadOnlyList<Vector2> vertices, int offset, int count, Vector2 position, Vector2 scale, bool reverse = false )
	{
		var firstIndex = _nextEdgeIndex;

		EnsureCapacity( count );
		Invalidate();

        var prevVertex = position + vertices[offset + count - 1] * scale;
		for ( var i = 0; i < count; ++i )
		{
			var nextVertex = position + vertices[offset + i] * scale;

			_activeEdges.Add( AddEdge( prevVertex, Helpers.NormalizeSafe( nextVertex - prevVertex ), _prevDistance ) );

			prevVertex = nextVertex;
		}

		var prevIndex = count - 1;
		for ( var i = 0; i < count; ++i )
		{
			ref var prevEdge = ref _allEdges[firstIndex + prevIndex];
			ref var nextEdge = ref _allEdges[firstIndex + i];

			if ( reverse )
			{
				ConnectEdges( ref nextEdge, ref prevEdge );
			}
			else
			{
				ConnectEdges( ref prevEdge, ref nextEdge );
			}

			prevIndex = i;
		}

		return this;
	}

	[ThreadStatic]
	private static Dictionary<int, int> AddEdges_VertexMap;

	/// <summary>
	/// Add a raw set of edges. Be careful to ensure that each loop of edges is fully closed.
	/// </summary>
	/// <param name="vertices">Positions of vertices to connect with edges.</param>
	/// <param name="edges">Indices of the start and end vertices of each edge.</param>
	public void AddEdges( IReadOnlyList<Vector2> vertices, IReadOnlyList<(int Prev, int Next)> edges )
	{
		AddEdges_VertexMap ??= new Dictionary<int, int>();
		AddEdges_VertexMap.Clear();

		EnsureCapacity( edges.Count );
		Invalidate();

        foreach ( var (i, j) in edges )
		{
			var prev = vertices[i];
			var next = vertices[j];

			var index = AddEdge( prev, Helpers.NormalizeSafe( next - prev ), _prevDistance );

			_activeEdges.Add( index );
			AddEdges_VertexMap.Add( i, index );
		}

		for ( var i = 0; i < edges.Count; ++i )
		{
			var edge = edges[i];

			ref var prev = ref _allEdges[AddEdges_VertexMap[edge.Prev]];
			ref var next = ref _allEdges[AddEdges_VertexMap[edge.Next]];

			ConnectEdges( ref prev, ref next );
		}
	}

	private static float LerpRadians( float a, float b, float t )
	{
		var delta = b - a;
		delta -= MathF.Floor( delta * (0.5f / MathF.PI) ) * MathF.PI * 2f;

		if ( delta > MathF.PI )
		{
			delta -= MathF.PI * 2f;
		}

		return a + delta * Math.Clamp( t, 0f, 1f );
	}

	private Vector4 GetTangent( Vector3 normal )
	{
		var tangent = Vector3.Cross( normal, new Vector3( 0f, 0f, 1f ) ).Normal;

		return new Vector4( tangent, 1f );
	}

	private (int Prev, int Next) AddVertices( ref Edge edge, bool forceMaxDistance = false )
	{
		if ( edge.Vertices.Prev > -1 )
		{
			return edge.Vertices;
		}

		var prevEdge = _allEdges[edge.PrevEdge];

		var index = _vertices.Count;
		var prevNormal = -prevEdge.Normal;
		var nextNormal = -edge.Normal;

		var t = forceMaxDistance ? 1f : (edge.Distance - _prevDistance) * _invDistance;
		var height = _prevHeight + t * (_nextHeight - _prevHeight);

		var pos = new Vector3( edge.Origin.x, edge.Origin.y, height );

		if ( SkipNormals || MathF.Abs( _nextHeight - _prevHeight ) <= 0.001f )
		{
			_vertices.Add( new(
				pos,
				new Vector3( 0f, 0f, 1f ),
				new Vector4( 1f, 0f, 0f, 1f ) ) );

			edge.Vertices = (index, index);
		}
		else
		{
			var angle = LerpRadians( _prevAngle, _nextAngle, t );
			var cos = MathF.Cos( angle );
			var sin = MathF.Sin( angle );

			if ( Vector2.Dot( prevNormal, nextNormal ) >= _minSmoothNormalDot )
			{
				var normal = new Vector3( (prevNormal.x + nextNormal.x) * cos, (prevNormal.y + nextNormal.y) * cos, sin * 2f ).Normal;

				_vertices.Add( new( pos, normal, GetTangent( normal ) ) );

				edge.Vertices = (index, index);
			}
			else
			{
				var normal0 = new Vector3( prevNormal.x * cos, prevNormal.y * cos, sin ).Normal;
				var normal1 = new Vector3( nextNormal.x * cos, nextNormal.y * cos, sin ).Normal;

				_vertices.Add( new( pos, normal0, GetTangent( normal0 ) ) );
				_vertices.Add( new( pos, normal1, GetTangent( normal1 ) ) );

				edge.Vertices = (index, index + 1);
			}
		}

		return edge.Vertices;
	}

	private void AddTriangle( int a, int b, int c )
	{
		_indices.Add( a );
		_indices.Add( b );
		_indices.Add( c );
	}

	/// <summary>
	/// Add faces on each active edge extending upwards by the given height.
	/// </summary>
	/// <param name="height">Total distance upwards, away from the plane of the polygon.</param>
	public PolygonMeshBuilder Extrude( float height )
	{
		return Bevel( 0f, height );
	}

	/// <summary>
	/// Add faces on each active edge extending inwards by the given width. This will close the mesh if <paramref name="width"/> is large enough.
	/// </summary>
	/// <param name="width">Total distance inwards.</param>
	public PolygonMeshBuilder Inset( float width )
	{
		return Bevel( width, 0f );
	}

	[ThreadStatic]
	private static Dictionary<int, int> Mirror_IndexMap;

	/// <summary>
	/// Mirrors all previously created faces. The mirror plane is normal to the Z axis, with a given distance from the origin.
	/// </summary>
	/// <param name="z">Distance of the mirror plane from the origin.</param>
	public PolygonMeshBuilder Mirror( float z = 0f )
	{
		Mirror_IndexMap ??= new Dictionary<int, int>();
		Mirror_IndexMap.Clear();

		_vertices.EnsureCapacity( _vertices.Count * 2 );
		_indices.EnsureCapacity( _indices.Count * 2 );

		var indexCount = _indices.Count;
		var vertexCount = _vertices.Count;

		for ( var i = 0; i < vertexCount; i++ )
		{
			var vertex = _vertices[i];
			var position = vertex.Position;
			var normal = vertex.Normal;
			var tangent = vertex.Tangent;

			if ( Math.Abs( position.z - z ) <= 0.001f && (SkipNormals || Math.Abs( normal.z ) <= 0.0001f && Math.Abs( tangent.z ) <= 0.0001f) )
			{
				Mirror_IndexMap.Add( i, i );
			}
			else
			{
				Mirror_IndexMap.Add( i, _vertices.Count );

				_vertices.Add( new(
					new Vector3( position.x, position.y, z * 2f - position.z ),
					new Vector3( normal.x, normal.y, -normal.z ),
					new Vector4( tangent.x, tangent.y, -tangent.z, tangent.w ) ) );
			}
		}

		for ( var i = 0; i < indexCount; i += 3 )
		{
			var a = Mirror_IndexMap[_indices[i + 0]];
			var b = Mirror_IndexMap[_indices[i + 1]];
			var c = Mirror_IndexMap[_indices[i + 2]];

			_indices.Add( a );
			_indices.Add( c );
			_indices.Add( b );
		}

		return this;
	}

	/// <summary>
	/// Perform successive <see cref="Bevel"/>s so that the edge of the polygon curves inwards in a quarter circle arc.
	/// </summary>
	/// <param name="radius">Radius of the arc.</param>
	/// <param name="faces">How many bevels to split the rounded edge into.</param>
	/// <param name="smooth">If true, use smooth normals rather than flat shading.</param>
	/// <param name="convex">If true, the faces will be pointing outwards from the center of the arc.</param>
	public PolygonMeshBuilder Arc( float radius, int faces, bool smooth = true, bool convex = true )
	{
		return Arc( radius, radius, faces, smooth, convex );
	}

	/// <summary>
	/// Perform successive <see cref="Bevel"/>s so that the edge of the polygon curves inwards in a quarter circle arc.
	/// </summary>
	/// <param name="width">Total distance inwards.</param>
	/// <param name="height">Total distance upwards, away from the plane of the polygon.</param>
	/// <param name="faces">How many bevels to split the rounded edge into.</param>
	/// <param name="smooth">If true, use smooth normals rather than flat shading.</param>
	/// <param name="convex">If true, the faces will be pointing outwards from the center of the arc.</param>
	public PolygonMeshBuilder Arc( float width, float height, int faces, bool smooth = true, bool convex = true )
	{
		var prevWidth = 0f;
		var prevHeight = 0f;
		var prevTheta = 0f;

		static float MapAngle( float theta, bool convex, bool positive )
		{
			var min = positive ? 0f : MathF.PI * 0.5f;
			return convex ? min + theta : min + MathF.PI * 0.5f - theta;
		}

		for ( var i = 0; i < faces; ++i )
		{
			var theta = MathF.PI * 0.5f * (i + 1f) / faces;

			var cos = MathF.Cos( theta );
			var sin = MathF.Sin( theta );

			var nextWidth = 1f - cos;
			var nextHeight = sin;

			if ( smooth )
			{
				if ( height >= 0f == convex )
				{
					Bevel( (nextWidth - prevWidth) * width,
						(nextHeight - prevHeight) * height,
						MapAngle( prevTheta, convex, height >= 0f ),
						MapAngle( theta, convex, height >= 0f ) );
				}
				else
				{
					Bevel( (nextHeight - prevHeight) * width,
						(nextWidth - prevWidth) * height,
						MapAngle( prevTheta, convex, height >= 0f ),
						MapAngle( theta, convex, height >= 0f ) );
				}
			}
			else
			{
				if ( height >= 0f == convex )
				{
					Bevel( (nextWidth - prevWidth) * width,
						(nextHeight - prevHeight) * height );
				}
				else
				{
					Bevel( (nextHeight - prevHeight) * width,
						(nextWidth - prevWidth) * height );
				}
			}

			prevWidth = nextWidth;
			prevHeight = nextHeight;
			prevTheta = theta;
		}

		return this;
	}

	public void DrawGizmos( float minDist, float maxDist )
	{
		foreach ( var index in _activeEdges )
		{
			var edge = _allEdges[index];
			var next = _allEdges[edge.NextEdge];

			var start = edge.Project( minDist );
			var end = next.Project( minDist );

			Gizmo.Draw.Line( start, end );

			var dist = (Gizmo.LocalCameraTransform.Position - (Vector3)start).Length;
			var textOffset = dist / 64f;
			var textPos = start + (end - start).Normal * textOffset - edge.Normal * textOffset;

			Gizmo.Draw.Text( edge.ToString(), new Transform( textPos ) );

			if ( minDist < maxDist )
			{
				// Gizmo.Draw.Line( edge.Project( minDist ), edge.Project( Math.Min( edge.MaxDistance, maxDist ) ) );
			}
		}
	}

	public static void RunDebugDump( string dump, float? width, bool fromSdf, int maxIterations )
	{
		var parsed = Json.Deserialize<DebugDump>( dump );

		if ( fromSdf && parsed.SdfData is not null )
		{
			var samples = Convert.FromBase64String( parsed.SdfData.Samples );
			var data = new Sdf2DArrayData( samples, parsed.SdfData.BaseIndex, parsed.SdfData.Size,
				parsed.SdfData.RowStride );

			using var writer = Sdf2DMeshWriter.Rent();

			writer.AddEdgeLoops( data, 0f );
			writer.DrawGizmos();

			return;
		}

		using var builder = Rent();

		parsed.Init( builder );

		Gizmo.Draw.Color = Color.White;
		builder.DrawGizmos( 0f, width ?? parsed.EdgeWidth );

		if ( width <= 0f ) return;

		parsed.Bevel( builder, width );

		Gizmo.Draw.Color = Color.Blue;
		builder.DrawGizmos( width ?? parsed.EdgeWidth, width ?? parsed.EdgeWidth );

		builder.Fill();
	}
}
