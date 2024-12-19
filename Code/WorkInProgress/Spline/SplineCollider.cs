using Sandbox.Diagnostics;
using System.Collections.Concurrent;
using System.Threading;

namespace Sandbox;

public sealed class SplineColliderComponent : ModelCollider, Component.ExecuteInEditor
{
	[Property] public SplineComponent Spline { get; set; }

	[Property]
	[Range( 0, 16 )]
	public int Subdivision
	{
		get => _subdivision; set
		{
			_subdivision = value;
			Rebuild();
		}
	}

	private int _subdivision = 1;

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
		subHulls.Clear();
		subMeshes.Clear();
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
		if ( subHulls.Count == 0 && subMeshes.Count == 0 )
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

			var RollStartEnd = new Vector2( rollAtStart, rollAtEnd );
			var WidthHeightScaleStartEnd = new Vector4( scaleAtStart.x, scaleAtStart.y, scaleAtEnd.x, scaleAtEnd.y );

			foreach ( var subMesh in subMeshes )
			{
				var generator = collisionGeneratorPool.Get().Result;
				generator.SetVertices( subMesh.Vertices );
				generator.DeformVertices( minInModelDir, sizeInModelDir, P1, P2, P3, RollStartEnd, WidthHeightScaleStartEnd, subMesh.PartTransform );
				collisionGeneratorPool.Return( generator );
				var vertices = generator.GetDeformedVertices();
				var shape = _PhysicsBody.AddMeshShape( vertices.ToList(), subMesh.Indices );
				shape.Surface = subMesh.Surface;
			}

			// hull
			foreach ( var subHull in subHulls )
			{
				var generator = collisionGeneratorPool.Get().Result;
				generator.SetVertices( subHull.Vertices );
				generator.DeformVertices( minInModelDir, sizeInModelDir, P1, P2, P3, RollStartEnd, WidthHeightScaleStartEnd, subHull.PartTransform );
				collisionGeneratorPool.Return( generator );
				var vertices = generator.GetDeformedVertices();
				var shape = _PhysicsBody.AddHullShape( segmentTransform.Position, segmentTransform.Rotation, vertices.ToList() );
				shape.Surface = subHull.Surface;
			}
		}

		IsDirty = false;
	}

	protected override IEnumerable<PhysicsShape> CreatePhysicsShapes( PhysicsBody targetBody )
	{
		if ( Model is null || Model.Physics is null )
			yield break;

		if ( Model.Physics.Parts.Count == 0 )
			yield break;

		if ( Spline is null )
			yield break;

		subMeshes.Clear();
		subHulls.Clear();

		var bodyTransform = targetBody.Transform.ToLocal( WorldTransform );

		foreach ( var part in Model.Physics.Parts )
		{
			// Bone transform
			var bx = bodyTransform.ToWorld( part.Transform );

			foreach ( var sphere in part.Spheres )
			{
				const int rings = 8;
				SubdivideSphere( rings, sphere.Sphere.Center, sphere.Sphere.Radius, sphere.Surface, bx );
			}

			foreach ( var capsule in part.Capsules )
			{
				const int rings = 8;
				SubdivideCapsule( rings, capsule.Capsule.CenterA, capsule.Capsule.CenterB, capsule.Capsule.Radius, capsule.Surface, bx );
			}

			foreach ( var hull in part.Hulls )
			{
				SubdivideHull( hull, Subdivision, hull.Surface, bx );
			}

			foreach ( var mesh in part.Meshes )
			{
                var triangles = mesh.GetTriangles().ToList();

				// TODO slow as fuck can be improved by getting the lists directly rather than the traingel objects
				// can be done once we are out of scene staging.
                var vertices = new List<Vector3>();
                var indices = new List<int>(triangles.Count * 3);
                var vertexMap = new Dictionary<Vector3, int>(new Vector3Comparer());

                foreach (var triangle in triangles)
                {
                    if (!vertexMap.ContainsKey(triangle.A))
                    {
                        vertexMap[triangle.A] = vertices.Count;
                        vertices.Add(triangle.A);
                    }
                    indices.Add(vertexMap[triangle.A]);

                    if (!vertexMap.ContainsKey(triangle.B))
                    {
                        vertexMap[triangle.B] = vertices.Count;
                        vertices.Add(triangle.B);
                    }
                    indices.Add(vertexMap[triangle.B]);

                    if (!vertexMap.ContainsKey(triangle.C))
                    {
                        vertexMap[triangle.C] = vertices.Count;
                        vertices.Add(triangle.C);
                    }
                    indices.Add(vertexMap[triangle.C]);
                }

                subMeshes.Add(new SubMesh
                {
                    Vertices = vertices,
                    Indices = indices,
                    Surface = mesh.Surface,
					PartTransform = bx
				} );
			}

			if ( part.Mass > 0 )
				targetBody.Mass = part.Mass;

			if ( part.OverrideMassCenter )
				targetBody.LocalMassCenter = part.MassCenterOverride;

			if ( part.LinearDamping > 0 )
				targetBody.LinearDamping = part.LinearDamping;

			if ( part.AngularDamping > 0 )
				targetBody.AngularDamping = part.AngularDamping;
		}

		UpdateCollisions();
	}

	private void SubdivideHull( PhysicsGroupDescription.BodyPart.HullPart hull, int ringCount, Surface surface, Transform transform )
	{
		var sizeInModelDir = Model.Bounds.Size.Dot( Vector3.Forward );
		var minProj = Model.Bounds.Center.Dot( Vector3.Forward ) - sizeInModelDir / 2;
		var maxProj = Model.Bounds.Center.Dot( Vector3.Forward ) + sizeInModelDir / 2;

		var allVertices = hull.GetPoints();

		var interval = sizeInModelDir / ringCount;
		var rings = new List<List<Vector3>>();

		const float tolerance = 0.01f;

		// Create rings and intersect with existing edges
		for ( int i = 0; i <= ringCount; i++ )
		{
			float ringProj = minProj + (i * interval);
			var ringVertices = new HashSet<Vector3>( new Vector3Comparer() );

			// Find intersections with existing edges
			foreach ( var line in hull.GetLines() )
			{
				float startProj = GetProjection( line.Start, Vector3.Forward );
				float endProj = GetProjection( line.End, Vector3.Forward );

				// Check if line crosses this ring plane
				if ( (startProj <= ringProj + tolerance && endProj >= ringProj - tolerance) ||
					(endProj <= ringProj + tolerance && startProj >= ringProj - tolerance) )
				{
					// Calculate intersection point
					float t = (ringProj - startProj) / (endProj - startProj);
					Vector3 intersection = Vector3.Lerp( line.Start, line.End, t );
					ringVertices.Add( intersection );
				}
			}

			if ( ringVertices.Count > 0 )
			{
				rings.Add( ringVertices.ToList() );
			}
		}

		for ( int i = 0; i < rings.Count - 1; i++ )
		{
			var currentGroup = new List<Vector3>();

			// Add current ring
			currentGroup.AddRange( rings[i] );

			// Add next ring
			currentGroup.AddRange( rings[i + 1] );

			// Find and add original vertices that fall between these rings
			float groupStart = GetProjection( rings[i][0], Vector3.Forward );
			float groupEnd = GetProjection( rings[i + 1][0], Vector3.Forward );

			foreach ( var vertex in allVertices )
			{
				float vertexProj = GetProjection( vertex, Vector3.Forward );

				if ( groupStart <= vertexProj + tolerance && groupEnd >= vertexProj - tolerance )
				{
					currentGroup.Add( vertex );
				}
			}

			subHulls.Add( new SubHull
			{
				Vertices = currentGroup.ToList(),
				Surface = surface,
				PartTransform = transform
			} );
		}
	}

	private void SubdivideSphere( int rings, Vector3 center, float radius, Surface surface, Transform transform )
	{
		var ringPoints = new List<Vector3>[rings];
		for ( int i = 0; i < rings; ++i )
		{
			ringPoints[i] = new List<Vector3>();
			for ( int j = 0; j < rings; ++j )
			{
				var u = j / (float)(rings - 1);
				var v = i / (float)(rings - 1);
				var t = 2.0f * MathF.PI * u;
				var p = MathF.PI * v;

				var point = new Vector3( center.x + (radius * MathF.Sin( p ) * MathF.Cos( t )),
										center.y + (radius * MathF.Sin( p ) * MathF.Sin( t )),
										center.z + (radius * MathF.Cos( p )) );

				ringPoints[i].Add( point );
			}
		}

		for ( int i = 0; i < rings - 1; ++i )
		{
			var currentGroup = new List<Vector3>();
			currentGroup.AddRange( ringPoints[i] );
			currentGroup.AddRange( ringPoints[i + 1] );

			subHulls.Add( new SubHull
			{
				Vertices = currentGroup,
				Surface = surface,
				PartTransform = transform
			} );
		}
	}

	private void SubdivideCapsule( int rings, Vector3 centerA, Vector3 centerB, float radius, Surface surface, Transform transform )
	{
		if ( rings < 4 )
		{
			throw new ArgumentException( "Number of rings must be at least 4 to form a valid capsule." );
		}

		var capsuleDir = (centerB - centerA);
		var length = capsuleDir.Length;
		var direction = capsuleDir.Normal;

		// Find perpendicular vectors to create the ring plane
		var right = Vector3.Cross( direction, Vector3.Up );
		if ( right.Length < 0.001f )
		{
			right = Vector3.Cross( direction, Vector3.Forward );
		}
		right = right.Normal;
		var up = Vector3.Cross( right, direction ).Normal;

		var ringPoints = new List<List<Vector3>>();

		var hemisphereRings = rings / 4;
		var cylinderRings = rings - (hemisphereRings * 2) + 1;
		var totalRings = hemisphereRings * 2 + cylinderRings;

		// Generate all rings
		for ( int i = 0; i < totalRings; ++i )
		{
			var currentRing = new List<Vector3>();

			bool isInHemisphereA = i < hemisphereRings;
			bool isInHemisphereB = i >= (totalRings - hemisphereRings);

			// Calculate the base position along the capsule axis
			Vector3 basePos;
			if ( isInHemisphereA )
			{
				float t = i / (float)hemisphereRings;
				float angle = (MathF.PI / 2) * (1 - t);
				basePos = centerA - direction * (radius * MathF.Cos( angle ));
			}
			else if ( isInHemisphereB )
			{
				float t = (i - (totalRings - hemisphereRings)) / (float)hemisphereRings;
				float angle = (MathF.PI / 2) * t;
				basePos = centerB + direction * (radius * MathF.Cos( angle ));
			}
			else
			{
				float t = (i - hemisphereRings) / (float)(cylinderRings - 1);
				basePos = Vector3.Lerp( centerA, centerB, t );
			}

			// Generate points around the ring
			for ( int j = 0; j < rings; ++j )
			{
				var angle = j / (float)(rings - 1) * MathF.PI * 2;

				float ringRadius = radius;
				if ( isInHemisphereA )
				{
					float t = i / (float)hemisphereRings;
					float hemAngle = (MathF.PI / 2) * (1 - t);
					ringRadius = radius * MathF.Sin( hemAngle );
				}
				else if ( isInHemisphereB )
				{
					float t = (i - (totalRings - hemisphereRings)) / (float)hemisphereRings;
					float hemAngle = (MathF.PI / 2) * t;
					ringRadius = radius * MathF.Sin( hemAngle );
				}

				var point = basePos +
					(right * MathF.Cos( angle ) * ringRadius) +
					(up * MathF.Sin( angle ) * ringRadius);

				currentRing.Add( point );
			}

			// For hemisphere B, insert at the beginning instead of adding to the end
			if ( isInHemisphereB )
			{
				ringPoints.Insert( totalRings - hemisphereRings, currentRing );
			}
			else
			{
				ringPoints.Add( currentRing );
			}
		}

		for ( int i = 0; i < ringPoints.Count - 1; i++ )
		{
			var currentGroup = new List<Vector3>();
			currentGroup.AddRange( ringPoints[i] );
			currentGroup.AddRange( ringPoints[i + 1] );

			subHulls.Add( new SubHull
			{
				Vertices = currentGroup,
				Surface = surface,
				PartTransform = transform
			} );
		}
	}

	private static float GetProjection( Vector3 v, Vector3 direction )
	{
		return Vector3.Dot( v, direction );
	}

	struct SubHull
	{
		public List<Vector3> Vertices;
		public Surface Surface;
		public Transform PartTransform;
	}

	struct SubMesh
	{
		public List<Vector3> Vertices;
		public List<int> Indices;
		public Surface Surface;
		public Transform PartTransform;
	}

	private List<SubHull> subHulls = new();
	private List<SubMesh> subMeshes = new();
}

// need to be less precise than default
class Vector3Comparer : IEqualityComparer<Vector3>
{
	private const float Tolerance = 0.1f;

	public bool Equals( Vector3 a, Vector3 b )
	{
		return a.AlmostEqual( b, Tolerance );
	}

	public int GetHashCode( Vector3 v )
	{
		unchecked
		{
			return v.GetHashCode();
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

	public HashSet<Vector3> GetDeformedVertices()
	{
		return outVertices;
	}

	public void DeformVertices( float MinInMeshDir, float SizeInMeshDir, Vector3 P1, Vector3 P2, Vector3 P3, Vector2 RollStartEnd, Vector4 WidthHeightScaleStartEnd, Transform SegmentTransform )
	{
		outVertices.EnsureCapacity( inVertices.Count );
		outVertices.Clear();
		for ( int i = 0; i < inVertices.Count; i++ )
		{
			outVertices.Add( SegmentTransform.PointToWorld( SplineModelRendererComponent.DeformVertex( inVertices[i], MinInMeshDir, SizeInMeshDir, P1, P2, P3, RollStartEnd, WidthHeightScaleStartEnd ) ) );
		}
	}

	private List<Vector3> inVertices = new();
	private HashSet<Vector3> outVertices = new( new Vector3Comparer() );
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
