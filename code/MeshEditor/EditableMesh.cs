
using Sandbox;
using System.Collections.Generic;
using System.Transactions;

public class EditableMesh
{

	public List<Vector3> DistinctPositions { get; set; }
	public List<SimpleVertex_S> Vertexes { get; set; }
	public List<int> Indices { get; set; }

	List<MeshPart> parts = new();

	public IEnumerable<MeshPart> Parts => parts;
	public IEnumerable<MeshPart> Selection => parts.Where( x => x.Selected );

	public Mesh Mesh { get; private set; }

	public Action OnMeshChanged;

	public void Rotate( IEnumerable<MeshPart> parts, Angles angles, Vector3 center )
	{
		var rotationMatrix = Matrix.CreateRotation( angles.ToRotation() );
		var transformationMatrix = Matrix.CreateTranslation( -center ) * rotationMatrix * Matrix.CreateTranslation( center );

		Transform( parts, transformationMatrix );
	}

	// todo: per axis scaling
	public void Scale( IEnumerable<MeshPart> parts, float scale, Vector3 center )
	{
		var scaleMatrix = Matrix.CreateScale( scale, center );
		Transform( parts, scaleMatrix );
	}

	public void Translate( IEnumerable<MeshPart> parts, Vector3 translation )
	{
		Transform( parts, Matrix.CreateTranslation( translation ) );
	}

	public void Transform( IEnumerable<MeshPart> parts, Matrix transformation )
	{
		if ( !parts?.Any() ?? false ) return;

		List<int> distinctPositions = new();

		foreach ( var part in parts )
		{
			switch ( part.Type )
			{
				case MeshPartTypes.Face:
					distinctPositions.Add( Vertexes[part.A].DistinctIndex );
					distinctPositions.Add( Vertexes[part.B].DistinctIndex );
					distinctPositions.Add( Vertexes[part.C].DistinctIndex );
					distinctPositions.Add( Vertexes[part.D].DistinctIndex );
					break;
				case MeshPartTypes.Vertex:
					distinctPositions.Add( Vertexes[part.A].DistinctIndex );
					break;
				case MeshPartTypes.Edge:
					distinctPositions.Add( Vertexes[part.A].DistinctIndex );
					distinctPositions.Add( Vertexes[part.B].DistinctIndex );
					break;
			}
		}

		if ( !distinctPositions.Any() ) return;

		foreach ( var pos in distinctPositions )
		{
			var currentPosition = DistinctPositions[pos];
			var newPosition = transformation.Transform( currentPosition );
			UpdateVertexPosition( pos, newPosition );
		}

		Refresh();

		OnMeshChanged?.Invoke();
	}

	public void FlattenFace( MeshPart face )
	{
		if ( face == null || face.Type != MeshPartTypes.Face ) return;

		var indices = new List<int> { face.A, face.B, face.C, face.D }.ToList();
		var vertices = new List<SimpleVertex_S>()
		{
			Vertexes[face.A],
			Vertexes[face.B],
			Vertexes[face.C],
			Vertexes[face.D],
		};
		var normal = CalculateAverageNormal( indices );
		var pointOnPlane = vertices[0];

		foreach ( var v in vertices )
		{
			var projectedPos = ProjectOntoPlane( v.Position, pointOnPlane.Position, normal );
			UpdateVertexPosition( v.DistinctIndex, projectedPos );
		}

		Refresh();

		Vector3 ProjectOntoPlane( Vector3 point, Vector3 planePoint, Vector3 planeNormal )
		{
			var toPoint = point - planePoint;
			var distance = Vector3.Dot( toPoint, planeNormal );
			return point - (planeNormal * distance);
		}
	}

	public void ExtrudeFace( MeshPart face, float distance )
	{
		if ( face == null || face.Type != MeshPartTypes.Face ) return;

		var originalVerts = new List<int> { face.A, face.B, face.C, face.D };

		SortFaceVertices( ref originalVerts );

		var normal = CalculateAverageNormal( originalVerts );
		var extrudedVerts = ExtrudeVertices( originalVerts, normal * distance );

		AddFaceIndices( extrudedVerts[0], extrudedVerts[1], extrudedVerts[2], extrudedVerts[3] );

		for ( int i = 0; i < originalVerts.Count; i++ )
		{
			int next = (i + 1) % originalVerts.Count;

			var sideFaceVertices = CreateSideFaceVertices( originalVerts[i], originalVerts[next], extrudedVerts[next], extrudedVerts[i], normal );

			AddFaceIndices( sideFaceVertices[0], sideFaceVertices[1], sideFaceVertices[2], sideFaceVertices[3] );
		}

		Delete( face );
		Refresh();
	}

	public void Delete( MeshPart part )
	{
		List<int> vertsToRemove = new();

		switch ( part.Type )
		{
			case MeshPartTypes.Face:
				vertsToRemove.Add( part.A );
				vertsToRemove.Add( part.B );
				vertsToRemove.Add( part.C );
				vertsToRemove.Add( part.D );
				break;
			case MeshPartTypes.Edge:
				vertsToRemove.Add( part.A );
				vertsToRemove.Add( part.B );
				break;
			case MeshPartTypes.Vertex:
				vertsToRemove.Add( part.A );
				break;
		}

		vertsToRemove.Sort( ( a, b ) => b.CompareTo( a ) );

		Indices.RemoveAll( x => vertsToRemove.Contains( x ) );

		foreach ( var vertexIndex in vertsToRemove )
		{
			Vertexes.RemoveAt( vertexIndex );

			for ( int i = 0; i < Indices.Count; i++ )
			{
				if ( Indices[i] > vertexIndex )
				{
					Indices[i]--;
				}
			}
		}

		Refresh();
	}

	public void RecalculateNormals()
	{
		for ( int i = 0; i < Vertexes.Count; i++ )
		{
			var v = Vertexes[i];
			v.Normal = 0;
			Vertexes[i] = v;
		}

		for ( int i = 0; i < Indices.Count; i += 3 )
		{
			int index1 = Indices[i];
			int index2 = Indices[i + 1];
			int index3 = Indices[i + 2];

			SimpleVertex_S vertex1 = Vertexes[index1];
			SimpleVertex_S vertex2 = Vertexes[index2];
			SimpleVertex_S vertex3 = Vertexes[index3];

			Vector3 faceNormal = CalculateFaceNormal( vertex1.Position, vertex2.Position, vertex3.Position );

			vertex1.Normal += faceNormal;
			vertex2.Normal += faceNormal;
			vertex3.Normal += faceNormal;

			Vertexes[index1] = vertex1;
			Vertexes[index2] = vertex2;
			Vertexes[index3] = vertex3;
		}

		for ( int i = 0; i < Vertexes.Count; i++ )
		{
			var v = Vertexes[i];
			if ( v.Normal == 0 ) continue;
			v.Normal = v.Normal.Normal;
			Vertexes[i] = v;
		}

		Vector3 CalculateFaceNormal( Vector3 v1, Vector3 v2, Vector3 v3 )
		{
			Vector3 a = v2 - v1;
			Vector3 b = v3 - v1;
			return Vector3.Cross( a, b ).Normal;
		}
	}

	public void RecalculateUVs()
	{
		for ( int i = 0; i < Vertexes.Count; i++ )
		{
			var v = Vertexes[i];
			v.Texcoord = GenerateUVCoordinates( v.Position, v.Normal, 1 / 32.0f );
			Vertexes[i] = v;
		}

		Vector2 GenerateUVCoordinates( Vector3 Q, Vector3 P, float uvscale )
		{
			var U = Vector3.Cross( P, new Vector3( 0, 0, 1 ) );
			if ( Vector3.Dot( U, U ) < 0.001 )
			{
				U = new Vector3( 1, 0, 0 );
			}
			else
			{
				U = U.Normal;
			}

			var V = Vector3.Cross( P, U ).Normal;
			var uv = new Vector2( Vector3.Dot( Q, U ), Vector3.Dot( Q, V ) ) * uvscale;

			return uv;
		}
	}

	public void RecalculateTangents()
	{
		for ( int i = 0; i < Vertexes.Count; i++ )
		{
			var v = Vertexes[i];
			v.Tangent = 0;
			Vertexes[i] = v;
		}

		for ( int i = 0; i < Indices.Count; i += 3 )
		{
			int index1 = Indices[i];
			int index2 = Indices[i + 1];
			int index3 = Indices[i + 2];

			SimpleVertex_S vertex1 = Vertexes[index1];
			SimpleVertex_S vertex2 = Vertexes[index2];
			SimpleVertex_S vertex3 = Vertexes[index3];

			Vector3 tangent = CalculateTangent( vertex1, vertex2, vertex3 );

			vertex1.Tangent += tangent;
			vertex2.Tangent += tangent;
			vertex3.Tangent += tangent;

			Vertexes[index1] = vertex1;
			Vertexes[index2] = vertex2;
			Vertexes[index3] = vertex3;
		}

		for ( int i = 0; i < Vertexes.Count; i++ )
		{
			var v = Vertexes[i];
			if ( v.Tangent == 0 ) continue;
			v.Tangent = v.Tangent.Normal;
			Vertexes[i] = v;
		}

		Vector3 CalculateTangent( SimpleVertex_S v1, SimpleVertex_S v2, SimpleVertex_S v3 )
		{
			Vector3 edge1 = v2.Position - v1.Position;
			Vector3 edge2 = v3.Position - v1.Position;

			Vector2 deltaUV1 = v2.Texcoord - v1.Texcoord;
			Vector2 deltaUV2 = v3.Texcoord - v1.Texcoord;

			float f = 1.0f / (deltaUV1.x * deltaUV2.y - deltaUV2.x * deltaUV1.y);

			Vector3 tangent = default;
			tangent.x = f * (deltaUV2.y * edge1.x - deltaUV1.y * edge2.x);
			tangent.y = f * (deltaUV2.y * edge1.y - deltaUV1.y * edge2.y);
			tangent.z = f * (deltaUV2.y * edge1.z - deltaUV1.y * edge2.z);

			return tangent;
		}
	}

	List<int> CreateSideFaceVertices( int v0, int v1, int v2, int v3, Vector3 faceNormal )
	{
		var newVertsIndices = new List<int>();
		int[] vertices = { v0, v1, v2, v3 };

		Vector3[] edgeVectors = new Vector3[4];
		edgeVectors[0] = Vertexes[v1].Position - Vertexes[v0].Position;
		edgeVectors[1] = Vertexes[v2].Position - Vertexes[v1].Position;
		edgeVectors[2] = Vertexes[v3].Position - Vertexes[v2].Position;
		edgeVectors[3] = Vertexes[v0].Position - Vertexes[v3].Position;

		for ( int i = 0; i < vertices.Length; i++ )
		{
			var index = vertices[i];
			var originalVertex = Vertexes[index];
			var normal = Vector3.Cross( edgeVectors[i], faceNormal ).Normal;

			var newVertex = new SimpleVertex_S
			{
				Position = originalVertex.Position,
				Normal = normal,
				DistinctIndex = originalVertex.DistinctIndex
			};

			Vertexes.Add( newVertex );
			newVertsIndices.Add( Vertexes.Count - 1 );
		}

		return newVertsIndices;
	}

	public Vector3 CalculateNormal( IEnumerable<MeshPart> parts )
	{
		var indices = new List<int>();

		foreach ( var part in parts )
		{
			switch ( part.Type )
			{
				case MeshPartTypes.Vertex:
					indices.Add( part.A );
					break;
				case MeshPartTypes.Edge:
					indices.Add( part.A );
					indices.Add( part.B );
					break;
				case MeshPartTypes.Face:
					indices.Add( part.A );
					indices.Add( part.B );
					indices.Add( part.C );
					indices.Add( part.D );
					break;
			}
		}

		indices = indices.Distinct().ToList();

		if ( !indices.Any() ) return default;

		return CalculateAverageNormal( indices );
	}

	public Vector3 CalculateCenter( IEnumerable<MeshPart> parts )
	{
		if ( parts == null || !parts.Any() ) return default;

		Vector3 center = default;

		foreach ( var part in parts )
		{
			center += CalculateCenter( part );
		}

		return center / parts.Count();
	}

	public BBox CalculateBounds( MeshPart part )
	{
		var result = new BBox();

		switch ( part.Type )
		{
			case MeshPartTypes.Vertex:
				result = result.AddPoint( Vertexes[part.A].Position );
				break;
			case MeshPartTypes.Edge:
				result = result.AddPoint( Vertexes[part.A].Position );
				result = result.AddPoint( Vertexes[part.B].Position );
				break;
			case MeshPartTypes.Face:
				result = result.AddPoint( Vertexes[part.A].Position );
				result = result.AddPoint( Vertexes[part.B].Position );
				result = result.AddPoint( Vertexes[part.C].Position );
				result = result.AddPoint( Vertexes[part.D].Position );
				break;
		}

		return result;
	}

	public Vector3 CalculateCenter( MeshPart part )
	{
		Vector3 center = default;
		int count = 0;

		switch ( part.Type )
		{
			case MeshPartTypes.Face:
				center += Vertexes[part.A].Position;
				center += Vertexes[part.B].Position;
				center += Vertexes[part.C].Position;
				center += Vertexes[part.D].Position;
				count += 4;
				break;
			case MeshPartTypes.Vertex:
				center += Vertexes[part.A].Position;
				count += 1;
				break;
			case MeshPartTypes.Edge:
				center += Vertexes[part.A].Position;
				center += Vertexes[part.B].Position;
				count += 2;
				break;
		}

		return center / count;
	}

	public void Refresh()
	{
		RecalculateNormals();
		RecalculateUVs();
		RecalculateTangents();

		Mesh = new Mesh( Material.Load( "materials/dev/reflectivity_30.vmat" ) );
		Mesh.CreateVertexBuffer<SimpleVertex>( Vertexes.Count, SimpleVertex.Layout, Vertexes.Select( x => (SimpleVertex)x ).ToArray() );
		Mesh.CreateIndexBuffer( Indices.Count, Indices.ToArray() );
		Mesh.Bounds = BBox.FromPoints( DistinctPositions );

		parts.Clear();
		parts.AddRange( FindQuads() );

		var edges = FindEdges();
		var distinctEdges = edges
			.GroupBy( edge =>
			{
				return new
				{
					Min = Math.Min( Vertexes[edge.A].DistinctIndex, Vertexes[edge.B].DistinctIndex ),
					Max = Math.Max( Vertexes[edge.A].DistinctIndex, Vertexes[edge.B].DistinctIndex )
				};
			} )
			.Select( group => group.First() )
			.ToList();

		parts.AddRange( distinctEdges );

		HashSet<Vector3> aafasd = new();
		for ( int i = 0; i < Vertexes.Count; i++ )
		{
			if ( aafasd.Contains( Vertexes[i].DistinctIndex ) )
				continue;

			aafasd.Add( Vertexes[i].DistinctIndex );
			parts.Add( new MeshPart()
			{
				A = i,
				Type = MeshPartTypes.Vertex
			} );
		}

		OnMeshChanged?.Invoke();
	}

	List<int> ExtrudeVertices( List<int> originalVerts, Vector3 direction )
	{
		var newVertsIndices = new List<int>();
		foreach ( var index in originalVerts )
		{
			var originalVertex = Vertexes[index];

			DistinctPositions.Add( originalVertex.Position + direction );

			var newVertex = new SimpleVertex_S
			{
				Position = originalVertex.Position + direction,
				Normal = direction.Normal,
				DistinctIndex = DistinctPositions.Count - 1
			};

			Vertexes.Add( newVertex );
			newVertsIndices.Add( Vertexes.Count - 1 );
		}
		return newVertsIndices;
	}

	Vector3 CalculateAverageNormal( List<int> vertIndices )
	{
		var normalSum = Vector3.Zero;
		foreach ( var index in vertIndices )
		{
			normalSum += Vertexes[index].Normal;
		}
		return normalSum.Normal;
	}

	void SortFaceVertices( ref List<int> vertIndices )
	{
		Vector3 centroid = Vector3.Zero;
		foreach ( var index in vertIndices )
		{
			centroid += Vertexes[index].Position;
		}
		centroid /= vertIndices.Count;

		var normal = CalculateAverageNormal( vertIndices );

		vertIndices.Sort( ( a, b ) =>
		{
			Vector3 dirToA = Vertexes[a].Position - centroid;
			Vector3 dirToB = Vertexes[b].Position - centroid;
			Vector3 cross = Vector3.Cross( dirToA, dirToB );

			return Math.Sign( Vector3.Dot( cross, normal ) * -1 );
		} );
	}

	void AddFaceIndices( int v0, int v1, int v2, int v3 )
	{
		Indices.Add( v0 );
		Indices.Add( v1 );
		Indices.Add( v2 );

		Indices.Add( v2 );
		Indices.Add( v3 );
		Indices.Add( v0 );
	}

	void UpdateVertexPosition( int distinctIndex, Vector3 newPosition )
	{
		DistinctPositions[distinctIndex] = newPosition;

		for ( int i = 0; i < Vertexes.Count; i++ )
		{
			var v = Vertexes[i];
			if ( v.DistinctIndex != distinctIndex ) continue;

			v.Position = newPosition;
			Vertexes[i] = v;
		}
	}

	IEnumerable<MeshPart> FindQuads()
	{
		var sharedEdges = new Dictionary<(int, int), int>();
		var quadFaces = new List<MeshPart>();

		for ( int i = 0; i < Indices.Count; i += 3 )
		{
			if ( i + 2 >= Indices.Count ) break;

			int[] triangleIndices = { Indices[i], Indices[i + 1], Indices[i + 2] };

			for ( int j = 0; j < 3; j++ )
			{
				int indexA = triangleIndices[j];
				int indexB = triangleIndices[(j + 1) % 3];

				var edge = (indexA < indexB) ? (indexA, indexB) : (indexB, indexA);

				if ( sharedEdges.ContainsKey( edge ) )
				{
					int otherTriangleIndex = sharedEdges[edge];

					int[] otherTriangleIndices = { Indices[otherTriangleIndex], Indices[otherTriangleIndex + 1], Indices[otherTriangleIndex + 2] };
					int thirdVertex = otherTriangleIndices.FirstOrDefault( v => !triangleIndices.Contains( v ) );
					int fourthVertex = triangleIndices.FirstOrDefault( v => !otherTriangleIndices.Contains( v ) );

					quadFaces.Add( new MeshPart() { A = indexA, B = indexB, C = thirdVertex, D = fourthVertex, Type = MeshPartTypes.Face } );
				}
				else
				{
					sharedEdges[edge] = i;
				}
			}
		}

		return quadFaces;
	}

	IEnumerable<MeshPart> FindEdges()
	{
		var uniqueEdges = new HashSet<(int, int)>();

		for ( int i = 0; i < Indices.Count; i += 3 )
		{
			if ( i + 2 >= Indices.Count ) break;

			int[] triangleIndices = { Indices[i], Indices[i + 1], Indices[i + 2] };

			for ( int j = 0; j < 3; j++ )
			{
				int indexA = triangleIndices[j];
				int indexB = triangleIndices[(j + 1) % 3];

				var edge = (indexA < indexB) ? (indexA, indexB) : (indexB, indexA);

				if ( !uniqueEdges.Add( edge ) )
				{
					uniqueEdges.Remove( edge );
				}
			}
		}

		return uniqueEdges.Select( x => new MeshPart() { A = x.Item1, B = x.Item2, Type = MeshPartTypes.Edge } );
	}

	public static EditableMesh Cube( Vector3 size )
	{
		var result = new EditableMesh();
		result.Vertexes = new();
		result.Indices = new();
		result.DistinctPositions = new List<Vector3>()
		{
			new Vector3(-0.5f, -0.5f, 0.5f) * size,
			new Vector3(-0.5f, 0.5f, 0.5f) * size,
			new Vector3(0.5f, 0.5f, 0.5f) * size,
			new Vector3(0.5f, -0.5f, 0.5f) * size,
			new Vector3(-0.5f, -0.5f, -0.5f) * size,
			new Vector3(-0.5f, 0.5f, -0.5f) * size,
			new Vector3(0.5f, 0.5f, -0.5f) * size,
			new Vector3(0.5f, -0.5f, -0.5f) * size,
		};

		var faceIndices = new int[]
		{
				0, 1, 2, 3,
				7, 6, 5, 4,
				0, 4, 5, 1,
				1, 5, 6, 2,
				2, 6, 7, 3,
				3, 7, 4, 0,
		};

		for ( var i = 0; i < 6; ++i )
		{
			for ( var j = 0; j < 4; ++j )
			{
				var vertexIndex = faceIndices[(i * 4) + j];
				var pos = result.DistinctPositions[vertexIndex];

				result.Vertexes.Add( new()
				{
					Position = pos,
					DistinctIndex = vertexIndex
				} );
			}

			result.Indices.Add( i * 4 + 0 );
			result.Indices.Add( i * 4 + 2 );
			result.Indices.Add( i * 4 + 1 );
			result.Indices.Add( i * 4 + 2 );
			result.Indices.Add( i * 4 + 0 );
			result.Indices.Add( i * 4 + 3 );
		}

		result.Refresh();

		return result;
	}

}

public class MeshPart
{

	public int A, B, C, D;
	public bool Selected;
	public MeshPartTypes Type;

}

public enum MeshPartTypes
{
	Vertex,
	Face,
	Edge
}

public struct SimpleVertex_S
{
	public Vector3 Position { get; set; }
	public Vector3 Normal { get; set; }
	public Vector3 Tangent { get; set; }
	public Vector2 Texcoord { get; set; }
	public int DistinctIndex { get; set; }

	public static explicit operator SimpleVertex( SimpleVertex_S vertex )
	{
		return new SimpleVertex
		{
			position = vertex.Position,
			normal = vertex.Normal,
			tangent = vertex.Tangent,
			texcoord = vertex.Texcoord
		};
	}
}
