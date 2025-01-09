using Sandbox.Diagnostics;
using System.Collections.Concurrent;
using System.Diagnostics.Contracts;
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

	[Property]
	public Rotation ModelRotation { get; set; } = Rotation.Identity;

	private Vector3 ModelForward => ModelRotation.Forward;

	[Property]
	public bool UseRotationMinimizingFrames
	{
		get => _useRotationMinimizingFrames;
		set
		{
			_useRotationMinimizingFrames = value;
			IsDirty = true;
		}
	}

	private bool _useRotationMinimizingFrames = false;

	private int _subdivision = 1;

	private bool IsDirty
	{
		get => _isDirty; set => _isDirty = value;
	}

	private bool _isDirty = true;

	[Property]
	public float Spacing
	{
		get => _spacing;
		set
		{
			_spacing = value;
			IsDirty = true;
		}
	}

	private float _spacing = 0f;

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

	// This is internal hack it for now
	private PhysicsBody _PhysicsBody => Rigidbody.IsValid() ? Rigidbody.PhysicsBody : KeyframeBody;

	private BBox? _physicPartBounds = null;

	private void UpdateCollisions()
	{
		// Ensure we have valid physics bounds
		if ( _physicPartBounds is null )
			return;

		var rotatedModelBounds = Model.Bounds.Rotate( ModelRotation );
		var sizeInModelDir = rotatedModelBounds.Size.Dot( Vector3.Forward );
		var minInModelDir = rotatedModelBounds.Center.Dot( Vector3.Forward ) - sizeInModelDir / 2;

		var splineLength = Spline.GetLength();
		var sizeWithSpacing = sizeInModelDir + Spacing;

		var meshesRequired = (int)Math.Ceiling( splineLength / sizeInModelDir );
		var meshesRequiredWithSpacing = (int)Math.Ceiling( splineLength / sizeWithSpacing );

		if ( meshesRequiredWithSpacing == 0 )
		{
			return;
		}

		// Calculate frames along the spline
		int framesPerMesh = 12; // Adjust as needed
		var totalFrames = meshesRequired * framesPerMesh + 1;

		var frames = UseRotationMinimizingFrames
			? SplineModelRendererComponent.CalculateRotationMinimizingTangentFrames( Spline, totalFrames )
			: SplineModelRendererComponent.CalculateTangentFramesUsingUpDir( Spline, totalFrames );

		// Clear existing shapes
		_PhysicsBody.ClearShapes();

		for ( var meshIndex = 0; meshIndex < meshesRequired; meshIndex++ )
		{
			float startDistance = meshIndex * sizeWithSpacing;
			float endDistance = startDistance + sizeInModelDir;


			// Deform meshes
			foreach ( var subMesh in subMeshes )
			{
				var deformedVertices = new List<Vector3>();
				foreach ( var vertex in subMesh.Vertices )
				{
					SplineModelRendererComponent.Deform( Spline, ModelRotation, vertex, Vector3.Up, new Vector4( Vector3.Up, 0f ), frames, startDistance, endDistance, minInModelDir, sizeInModelDir, out var deformedVertex, out var deformedNormal, out var deformedTangent );
					deformedVertices.Add( deformedVertex );
				}
				var shape = _PhysicsBody.AddMeshShape( deformedVertices, subMesh.Indices );
				shape.Surface = subMesh.Surface;
			}

			// Deform hulls
			foreach ( var subHull in subHulls )
			{
				var deformedVertices = new List<Vector3>();
				foreach ( var vertex in subHull.Vertices )
				{
					SplineModelRendererComponent.Deform( Spline, ModelRotation, vertex, Vector3.Up, new Vector4( Vector3.Up, 0f ), frames, startDistance, endDistance, minInModelDir, sizeInModelDir, out var deformedVertex, out var deformedNormal, out var deformedTangent );
					deformedVertices.Add( deformedVertex );
				}
				var shape = _PhysicsBody.AddHullShape( Vector3.Zero, Rotation.Identity, deformedVertices );
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

		_physicPartBounds = null;

		var bodyTransform = targetBody.Transform.ToLocal( WorldTransform );

		foreach ( var part in Model.Physics.Parts )
		{
			// Bone transform
			var bx = bodyTransform.ToWorld( part.Transform );

			foreach ( var sphere in part.Spheres )
			{
				const int rings = 8;
				SubdivideSphere( rings, sphere.Sphere.Center, sphere.Sphere.Radius, sphere.Surface, bx );

				var sphereBounds = new BBox( sphere.Sphere.Center - new Vector3( sphere.Sphere.Radius ), sphere.Sphere.Center + new Vector3( sphere.Sphere.Radius ) );

				sphereBounds = sphereBounds.Transform( bx );

				_physicPartBounds = _physicPartBounds?.AddBBox( sphereBounds ) ?? sphereBounds;
			}

			foreach ( var capsule in part.Capsules )
			{
				var rotatedCenterA = bx.PointToWorld( capsule.Capsule.CenterA ) * ModelRotation;
				var rotatedCenterB = bx.PointToWorld( capsule.Capsule.CenterB ) * ModelRotation;
				SubdivideCapsule( 4 + Subdivision, rotatedCenterA, rotatedCenterB, capsule.Capsule.Radius, capsule.Surface );

				var capsuleBounds = BBox.FromPoints(
					new Vector3[] {
						rotatedCenterA - new Vector3( capsule.Capsule.Radius ),
						rotatedCenterA + new Vector3( capsule.Capsule.Radius ),
						rotatedCenterB - new Vector3( capsule.Capsule.Radius ),
						rotatedCenterB + new Vector3( capsule.Capsule.Radius )
					} );

				_physicPartBounds = _physicPartBounds?.AddBBox( capsuleBounds ) ?? capsuleBounds;
			}

			foreach ( var hull in part.Hulls )
			{
				SubdivideHull( hull, Subdivision + 1, hull.Surface, bx );

				var hullBounds = hull.Bounds.Transform( bx ).Rotate( ModelRotation );

				_physicPartBounds = _physicPartBounds?.AddBBox( hullBounds ) ?? hullBounds;
			}

			foreach ( var mesh in part.Meshes )
			{
                var triangles = mesh.GetTriangles().ToList();

				// TODO slow as fuck can be improved by getting the lists directly rather than the traingle objects
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

				var meshBounds = mesh.Bounds.Transform( bx );

				_physicPartBounds = _physicPartBounds?.AddBBox( meshBounds ) ?? meshBounds;
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

		// Transform all the hull vertices once
		// TODO can optimzie this all by getting vertices and lines directly from hull an transforming than once
		// instead of this LINQ madness, will do once out of scenestaging.
		List<Vector3> transformedVertices = hull.GetPoints().Select( vertex => transform.PointToWorld( vertex ) * ModelRotation ).ToList();

		if ( ringCount == 0 )
		{
			subHulls.Add( new SubHull
			{
				Vertices = transformedVertices,
				Surface = surface,
			} );
			return;
		}

		// Get the transformed edges
		var transformedLines = hull.GetLines().Select( line => new Line(
		transform.PointToWorld( line.Start ) * ModelRotation,
		transform.PointToWorld( line.End )  * ModelRotation)).ToList();

		// Project all vertices along Vector3.Forward
		float minProj = transformedVertices.Min( vertex => Vector3.Dot( vertex, Vector3.Forward ) );
		float maxProj = transformedVertices.Max( vertex => Vector3.Dot( vertex, Vector3.Forward ) );

		float sizeInModelDir = maxProj - minProj;
		float interval = sizeInModelDir / ringCount;

		const float tolerance = 0.01f;

		List<List<Vector3>> rings = new List<List<Vector3>>();

		// Create rings and intersect with existing edges
		for ( int i = 0; i <= ringCount; i++ )
		{
			float ringProj = minProj + (i * interval);
			var ringVertices = new HashSet<Vector3>( new Vector3Comparer() );

			// Find intersections with existing edges
			foreach ( var line in transformedLines )
			{
				float startProj = Vector3.Dot( line.Start, Vector3.Forward );
				float endProj = Vector3.Dot( line.End, Vector3.Forward );

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

		// Group rings into SubHulls
		for ( int i = 0; i < rings.Count - 1; i++ )
		{
			var currentGroup = new List<Vector3>();

			// Add current ring
			currentGroup.AddRange( rings[i] );

			// Add next ring
			currentGroup.AddRange( rings[i + 1] );

			// Find and add original vertices that fall between these rings
			float groupStart = rings[i].Min( v => Vector3.Dot( v, Vector3.Forward ) );
			float groupEnd = rings[i + 1].Max( v => Vector3.Dot( v, Vector3.Forward ) );

			foreach ( var vertex in transformedVertices )
			{
				float vertexProj = Vector3.Dot( vertex, Vector3.Forward );

				if ( vertexProj >= groupStart - tolerance && vertexProj <= groupEnd + tolerance )
				{
					currentGroup.Add( vertex );
				}
			}

			subHulls.Add( new SubHull
			{
				Vertices = currentGroup,
				Surface = surface,
			} );
		}
	}


	private void SubdivideSphere( int rings, Vector3 center, float radius, Surface surface, Transform transform )
	{
		var ringPoints = new List<Vector3>[rings];

		Vector3 direction = ModelForward;

		// Find two vectors orthogonal to ModelForward to form a coordinate system
		// TODO there are better ways todo this
		Vector3 right = Vector3.Cross( direction, Vector3.Up );
		if ( right.Length < 0.001f )
		{
			right = Vector3.Cross( direction, Vector3.Right );
		}
		right = right.Normal;
		Vector3 up = Vector3.Cross( right, direction ).Normal;

		for ( int i = 0; i < rings; ++i )
		{
			ringPoints[i] = new List<Vector3>();
			float v = i / (float)(rings - 1);
			float theta = v * MathF.PI; // Angle from 0 to PI

			float sinTheta = MathF.Sin( theta );
			float cosTheta = MathF.Cos( theta );

			for ( int j = 0; j < rings; ++j )
			{
				float u = j / (float)(rings - 1);
				float phi = u * 2.0f * MathF.PI;

				float sinPhi = MathF.Sin( phi );
				float cosPhi = MathF.Cos( phi );

				// Convert spherical coordinates to Cartesian coordinates aligned along ModelForward
				Vector3 point = center
					+ (right * (sinTheta * cosPhi * radius))
					+ (up * (sinTheta * sinPhi * radius))
					+ (direction * (cosTheta * radius));

				ringPoints[i].Add( transform.PointToWorld( point ) );
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
			} );
		}
	}

	private void SubdivideCapsule( int totalRings, Vector3 centerA, Vector3 centerB, float radius, Surface surface )
	{
		const int segments = 8;
		if ( totalRings < 4 )
		{
			throw new ArgumentException( "Number of rings must be at least 4 to form a valid capsule." );
		}

		// Generate all rings along the capsule
		List<List<Vector3>> ringPoints = new();
		GenerateCapsuleRings( centerA, centerB, radius, totalRings, segments, ringPoints );

		// Group the rings into SubHulls
		for ( int i = 0; i < ringPoints.Count - 1; i++ )
		{
			var currentGroup = new List<Vector3>();
			currentGroup.AddRange( ringPoints[i] );
			currentGroup.AddRange( ringPoints[i + 1] );

			subHulls.Add( new SubHull
			{
				Vertices = currentGroup,
				Surface = surface,
			} );
		}
	}

	private void GenerateCapsuleRings( Vector3 centerA, Vector3 centerB, float radius, int totalRings, int segments, List<List<Vector3>> ringPoints )
	{
		Vector3 direction = (centerB - centerA).Normal;
		float cylinderHeight = (centerB - centerA).Length;

		float totalHeight = cylinderHeight + 2 * radius;

		// Build orthonormal basis
		// TODO there are better ways todo this
		Vector3 up = direction;
		Vector3 right = Vector3.Cross( direction, Vector3.Up );
		if ( right.LengthSquared < 0.001f )
		{
			right = Vector3.Cross( direction, Vector3.Right );
		}
		right = right.Normal;
		Vector3 forward = Vector3.Cross( right, up ).Normal;

		for ( int i = 0; i <= totalRings; i++ )
		{
			float v = i / (float)totalRings;
			float h = v * totalHeight;

			List<Vector3> currentRing = new();

			for ( int j = 0; j <= segments; j++ )
			{
				float u = j / (float)segments;
				float phi = u * 2.0f * MathF.PI;

				float sinPhi = MathF.Sin( phi );
				float cosPhi = MathF.Cos( phi );

				Vector3 point;

				if ( h < radius )
				{
					// Bottom hemisphere
					float t = h / radius; // t from 0 to 1
					float theta = (MathF.PI / 2) + (1 - t) * (MathF.PI / 2); // theta from π to π/2

					float sinTheta = MathF.Sin( theta );
					float cosTheta = MathF.Cos( theta );

					point = centerA
						+ (right * (sinTheta * cosPhi * radius))
						+ (forward * (sinTheta * sinPhi * radius))
						+ (up * (cosTheta * radius));
				}
				else if ( h <= radius + cylinderHeight )
				{
					// Cylinder
					float y = h - radius; // y from 0 to cylinderHeight

					point = centerA
						+ up * y
						+ (right * (cosPhi * radius))
						+ (forward * (sinPhi * radius));
				}
				else
				{
					// Top hemisphere
					float t = (h - (radius + cylinderHeight)) / radius; // t from 0 to 1
					float theta = (MathF.PI / 2) * (1 - t); // theta from π/2 to 0

					float sinTheta = MathF.Sin( theta );
					float cosTheta = MathF.Cos( theta );

					point = centerB
						+ (right * (sinTheta * cosPhi * radius))
						+ (forward * (sinTheta * sinPhi * radius))
						+ (up * (cosTheta * radius));
				}

				currentRing.Add( point );
			}

			ringPoints.Add( currentRing );
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
