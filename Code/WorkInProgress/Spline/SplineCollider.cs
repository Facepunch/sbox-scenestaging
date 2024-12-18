using Sandbox.Diagnostics;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;

namespace Sandbox;

public sealed class SplineColliderComponent : ModelCollider, Component.ExecuteInEditor
{
	[Property] public SplineComponent Spline { get; set; }

	private static SplineCollisionGeneratorPool collisionGeneratorPool = new( 100 );

	private bool IsDirty
	{
		get => _isDirty; set => _isDirty = value;
	}

	private bool _isDirty = true;

	protected override void OnEnabled()
	{
		if ( Model.IsValid() && Spline.IsValid() )
		{
			IsDirty = true;
			Spline.SplineChanged += MarkDirty;
		}
		base.OnEnabled();
	}

	private void MarkDirty()
	{
		IsDirty = true;
	}

	protected override void OnDisabled()
	{
		Spline.SplineChanged -= MarkDirty;
		meshVertices.Clear();
		meshIndices.Clear();
		subHullVertices.Clear();
		base.OnDisabled();
	}

	protected override void OnUpdate()
	{
		if ( !Model.IsValid() || !Spline.IsValid() )
		{
			return;
		}

		if ( !Spline.IsDirty && !IsDirty )
		{
			return;
		}

		// Nothing to deform
		if ( meshVertices.Count == 0 && subHullVertices.Count == 0 )
		{
			return;
		}

		UpdateCollisions();
	}

	// This is internal hack it for not
	private PhysicsBody _PhysicsBody => Rigidbody.IsValid() ? Rigidbody.PhysicsBody : KeyframeBody;


	private void UpdateCollisions()
	{
		var sizeInModelDir = Model.Bounds.Size.Dot( Vector3.Forward );

		var minInModelDir = Model.Bounds.Center.Dot( Vector3.Forward ) - sizeInModelDir / 2;

		var meshesRequired = (int)Math.Round( Spline.GetLength() / sizeInModelDir );
		var distancePerCurve = Spline.GetLength() / meshesRequired;

		_PhysicsBody.ClearShapes();

		for ( var meshIndex = 0; meshIndex < meshesRequired; meshIndex++ )
		{
			var P0 = Spline.GetPositionAtDistance( meshIndex * distancePerCurve );
			var P1 = P0 + Spline.GetTangetAtDistance( meshIndex * distancePerCurve ) * distancePerCurve / 3;
			var P3 = Spline.GetPositionAtDistance( (meshIndex + 1) * distancePerCurve );
			var P2 = P3 - Spline.GetTangetAtDistance( (meshIndex + 1) * distancePerCurve ) * distancePerCurve / 3;

			var segmentTransform = new Transform( P0, Rotation.LookAt( P3 - P0 ) );
			segmentTransform.Rotation = new Angles( 0, segmentTransform.Rotation.Yaw(), 0 ).ToRotation();

			var rollAtStart = MathX.DegreeToRadian( Spline.GetRollAtDistance( meshIndex * distancePerCurve ) );
			var rollAtEnd = MathX.DegreeToRadian( Spline.GetRollAtDistance( (meshIndex + 1) * distancePerCurve ) );

			var scaleAtStart = Spline.GetScaleAtDistance( meshIndex * distancePerCurve );
			var scaleAtEnd = Spline.GetScaleAtDistance( (meshIndex + 1) * distancePerCurve );

			P1 = new Vector4( segmentTransform.PointToLocal( P1 ) );
			P2 = new Vector4( segmentTransform.PointToLocal( P2 ) );
			P3 = new Vector4( segmentTransform.PointToLocal( P3 ) );

			var RollStartEnd = new Vector2( rollAtStart, rollAtEnd);
			var WidthHeightScaleStartEnd = new Vector4( scaleAtStart.x, scaleAtStart.y, scaleAtEnd.x, scaleAtEnd.y );

			// mesh
			//var generator = collisionGeneratorPool.Get().Result;
			//generator.SetVertices( meshVertices );
			//generator.DeformVertices( minInModelDir, sizeInModelDir, P1, P2, P3, RollStartEnd, WidthHeightScaleStartEnd, segmentTransform );
			//collisionGeneratorPool.Return( generator );

			//KeyframeBody.AddMeshShape( generator.GetDeformedVertices(), meshIndices );

			// hull
			foreach ( var subHull in subHullVertices )
			{
				var generator = collisionGeneratorPool.Get().Result;
				generator.SetVertices( subHull );
				generator.DeformVertices( minInModelDir, sizeInModelDir, P1, P2, P3, RollStartEnd, WidthHeightScaleStartEnd, new Transform() );
				collisionGeneratorPool.Return( generator );
				_PhysicsBody.AddHullShape( segmentTransform.Position, segmentTransform.Rotation, generator.GetDeformedVertices() );
			}
		}

		IsDirty = false;
	}

	protected override void RebuildImmediately()
	{
		base.RebuildImmediately();

		if ( !Model.IsValid() || !Spline.IsValid() )
		{
			return;
		}

		meshVertices.Clear();
		subHullVertices.Clear();

		var sizeInModelDir = Model.Bounds.Size.Dot( Vector3.Forward );

		var minInModelDir = Model.Bounds.Center.Dot( Vector3.Forward ) - sizeInModelDir / 2;

		var meshesRequired = (int)Math.Round( Spline.GetLength() / sizeInModelDir );
		var distancePerCurve = Spline.GetLength() / meshesRequired;
		float maxProjectedEdgeLength = distancePerCurve / 4;


		// TODO there are nicer ways to get the vertices, but that requires internal access and soem engine changes
		// as long as we are in scenestaging hack it
		foreach ( var shape in this._PhysicsBody.Shapes )
		{
			if ( shape.IsMeshShape )
			{
				shape.Triangulate( out var vertices, out var indices );
				meshVertices.AddRange( vertices );
				for ( int i = 0; i < indices.Length; i++ )
				{
					meshIndices.Add( (int)indices[i] );
				}
			}
			else
			{

				// Hulls/spheres/capsules may have to little geoemtry to deform them properly
				// Subdidivide them and split into multiple hulls
				shape.Triangulate( out var verticesArray, out var indicesArray );


				// Create a HashSet with custom comparer
				var subdividedVertices = new HashSet<Vector3>( new Vector3Comparer() );

				// Subdivide the triangles
				SubdivideTrianglesByProjectedEdgeLength(
					verticesArray,
					indicesArray,
					Vector3.Forward,
					maxProjectedEdgeLength,
					subdividedVertices );

				// Convert the HashSet to a sorted list
				var sortedVertices = subdividedVertices
					.OrderBy( v => GetProjection( v, Vector3.Forward ) )
					.ToList();

				// Now split the hull vertices into overlapping groups

				float groupLength = maxProjectedEdgeLength * 2;
				float overlapPercentage = 0.25f; // 10% overlap between groups
				float overlapLength = groupLength * overlapPercentage;

				int vertexCount = sortedVertices.Count;
				int i = 0;

				while ( i < vertexCount )
				{
					List<Vector3> currentGroup = new();

					// Get the start projection for the current group
					float groupStartProjection = GetProjection( sortedVertices[i], Vector3.Forward );
					float groupEndProjection = groupStartProjection + groupLength;

					// Collect vertices for the current group
					int startIndex = i;
					while ( i < vertexCount && GetProjection( sortedVertices[i], Vector3.Forward ) <= groupEndProjection )
					{
						currentGroup.Add( sortedVertices[i] );
						i++;
					}

					// Add current group to subHullVertices
					subHullVertices.Add( currentGroup );

					if ( overlapLength > 0 && i < vertexCount )
					{
						// Find the starting index for the next group
						float nextGroupStartProjection = groupEndProjection - overlapLength;

						// Move i back to include overlap
						int j = startIndex;
						while ( j < i && GetProjection( sortedVertices[j], Vector3.Forward ) < nextGroupStartProjection )
						{
							j++;
						}
						i = j;
					}
				}
			}
		}

		MarkDirty();
	}

	private static float GetProjection( Vector3 v, Vector3 direction )
	{
		return Vector3.Dot( v, direction );
	}

	private List<Vector3> meshVertices = new();
	private List<int> meshIndices = new();

	private List<List<Vector3>> subHullVertices = new();

	public void SubdivideTrianglesByProjectedEdgeLength(
		Vector3[] vertices,
		uint[] indices,
		Vector3 direction,
		float maxProjectedEdgeLength,
		HashSet<Vector3> subdividedVertices )
	{
		// Normalize the direction vector
		direction = direction.Normal;

		// Initialize queue with original triangles represented by their vertex indices
		Queue<(int i0, int i1, int i2)> triangleQueue = new();

		// Enqueue the initial triangles using indices
		for ( int i = 0; i < indices.Length; i += 3 )
		{
			int i0 = (int)indices[i];
			int i1 = (int)indices[i + 1];
			int i2 = (int)indices[i + 2];
			triangleQueue.Enqueue( (i0, i1, i2) );
		}

		// Dictionary to cache midpoints and avoid duplicate calculations
		Dictionary<(int, int), int> midpointCache = new();

		// Create a list to hold all vertices, including new midpoints
		List<Vector3> allVertices = new( vertices );

		while ( triangleQueue.Count > 0 )
		{
			var (i0, i1, i2) = triangleQueue.Dequeue();

			Vector3 v0 = allVertices[i0];
			Vector3 v1 = allVertices[i1];
			Vector3 v2 = allVertices[i2];

			// Check projected edge lengths
			bool needsSubdivision = false;

			// Check each edge
			if ( GetProjectedEdgeLength( v0, v1, direction ) > maxProjectedEdgeLength ||
				 GetProjectedEdgeLength( v1, v2, direction ) > maxProjectedEdgeLength ||
				 GetProjectedEdgeLength( v2, v0, direction ) > maxProjectedEdgeLength )
			{
				needsSubdivision = true;
			}

			if ( needsSubdivision )
			{
				// Get or create midpoints
				int m0 = GetOrCreateMidpoint( i0, i1, allVertices, midpointCache );
				int m1 = GetOrCreateMidpoint( i1, i2, allVertices, midpointCache );
				int m2 = GetOrCreateMidpoint( i2, i0, allVertices, midpointCache );

				// Enqueue the four new triangles for further subdivision
				triangleQueue.Enqueue( (i0, m0, m2) );
				triangleQueue.Enqueue( (m0, i1, m1) );
				triangleQueue.Enqueue( (m2, m1, i2) );
				triangleQueue.Enqueue( (m0, m1, m2) );
			}
			else
			{
				// Triangle meets the criterion; add its vertices to the HashSet
				subdividedVertices.Add( v0 );
				subdividedVertices.Add( v1 );
				subdividedVertices.Add( v2 );
			}
		}
	}

	private int GetOrCreateMidpoint(
		int indexA,
		int indexB,
		List<Vector3> vertexList,
		Dictionary<(int, int), int> midpointCache )
	{
		// Ensure consistent ordering to avoid duplicate edges
		var key = indexA < indexB ? (indexA, indexB) : (indexB, indexA);

		if ( !midpointCache.TryGetValue( key, out int midpointIndex ) )
		{
			var midpoint = (vertexList[indexA] + vertexList[indexB]) * 0.5f;
			midpointIndex = vertexList.Count;
			vertexList.Add( midpoint );
			midpointCache[key] = midpointIndex;
		}
		return midpointIndex;
	}

	private float GetProjectedEdgeLength( Vector3 vStart, Vector3 vEnd, Vector3 direction )
	{
		Vector3 edgeVector = vEnd - vStart;
		float projectedLength = MathF.Abs( Vector3.Dot( edgeVector, direction ) );
		return projectedLength;
	}

	class Vector3Comparer : IEqualityComparer<Vector3>
	{
		private const float Tolerance = 1e-1f;

		public bool Equals( Vector3 a, Vector3 b )
		{
			return (a - b).LengthSquared <= Tolerance * Tolerance;
		}

		public int GetHashCode( Vector3 v )
		{
			unchecked
			{
				int hashX = MathF.Round( v.x / Tolerance ).GetHashCode();
				int hashY = MathF.Round( v.y / Tolerance ).GetHashCode();
				int hashZ = MathF.Round( v.z / Tolerance ).GetHashCode();
				return hashX * 397 ^ hashY * 397 ^ hashZ;
			}
		}
	}
}

class SplineCollisionGenerator
{
	public void SetVertices( List<Vector3> vertices )
	{
		inVertices.Clear();
		inVertices.AddRange( vertices );
	}

	public List<Vector3> GetDeformedVertices()
	{
		return outVertices;
	}

	public void DeformVertices( float MinInMeshDir, float SizeInMeshDir, Vector3 P1, Vector3 P2, Vector3 P3, Vector2 RollStartEnd, Vector4 WidthHeightScaleStartEnd, Transform SegmentTransform )
	{
		outVertices.Capacity = Math.Max( outVertices.Capacity, inVertices.Count );
		outVertices.Clear();
		for ( int i = 0; i < inVertices.Count; i++ )
		{
			outVertices.Add( SegmentTransform.PointToWorld( SplineModelRendererComponent.DeformVertex( inVertices[i], MinInMeshDir, SizeInMeshDir, P1, P2, P3, RollStartEnd, WidthHeightScaleStartEnd ) ) );
		}
	}

	private List<Vector3> inVertices = new();
	private List<Vector3> outVertices = new();
}


class SplineCollisionGeneratorPool
{

	private readonly ConcurrentBag<SplineCollisionGenerator> generatorInstances;

	private readonly int _maxPoolSize;

	private SemaphoreSlim generatorSemaphore;


	public SplineCollisionGeneratorPool( int maxPoolSize )
	{
		generatorInstances = new ConcurrentBag<SplineCollisionGenerator>();
		generatorSemaphore = new SemaphoreSlim( maxPoolSize );
		_maxPoolSize = maxPoolSize;
		for ( int i = 0; i < _maxPoolSize; i++ )
		{
			generatorInstances.Add( new SplineCollisionGenerator() );
		}
	}

	public async Task<SplineCollisionGenerator> Get()
	{
		ThreadSafe.AssertIsMainThread();
		await generatorSemaphore.WaitAsync();
		var succeeded = generatorInstances.TryTake( out SplineCollisionGenerator generatorInstance );
		Assert.True( succeeded );

		return generatorInstance;
	}

	public void Return( SplineCollisionGenerator generatorInstance )
	{
		ThreadSafe.AssertIsMainThread();
		// not worth to do in parallel it's too fast
		generatorInstances.Add( generatorInstance );
		generatorSemaphore.Release();
	}
}
