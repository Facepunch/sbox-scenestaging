using Sandbox;

public partial class Scene : GameObject
{
	public SceneTrace Trace => new SceneTrace( this );


	internal SceneTraceResult RunTrace( in SceneTrace trace )
	{
		var physicsResult = trace.PhysicsTrace.Run();
		SceneTraceResult sceneResult = SceneTraceResult.From( this, physicsResult );

		// pool me
		List<SceneTraceResult> results = new List<SceneTraceResult>();
		results.Add( sceneResult );

		foreach ( var system in systems )
		{
			if ( system is GameObjectSystem.ITraceProvider traceProvider )
			{
				traceProvider.DoTrace( trace, results );
			}
		}

		foreach ( var result in results.OrderBy( x => x.Fraction ) )
		{
			return result;
		}

		return sceneResult;
	}
}


public partial struct SceneTrace
{
	Scene scene;
	public PhysicsTraceBuilder PhysicsTrace;
	public bool IncludeHitboxes;

	internal SceneTrace( Scene scene )
	{
		this.scene = scene;
		PhysicsTrace = GameManager.ActiveScene.PhysicsWorld.Trace;
	}

	/// <summary>
	/// Casts a sphere from point A to point B.
	/// </summary>
	public SceneTrace Sphere( float radius, in Vector3 from, in Vector3 to ) => Ray( from, to ).Radius( radius );

	/// <summary>
	/// Casts a sphere from a given position and direction, up to a given distance.
	/// </summary>
	public SceneTrace Sphere( float radius, in Ray ray, in float distance ) => Ray( ray, distance ).Radius( radius );

	/// <summary>
	/// Casts a box from point A to point B.
	/// </summary>
	public SceneTrace Box( Vector3 extents, in Vector3 from, in Vector3 to )
	{
		return Ray( from, to ).Size( extents );
	}

	/// <summary>
	/// Casts a box from a given position and direction, up to a given distance.
	/// </summary>
	public SceneTrace Box( Vector3 extents, in Ray ray, in float distance )
	{
		return Ray( ray, distance ).Size( extents );
	}

	/// <summary>
	/// Casts a box from point A to point B.
	/// </summary>
	public SceneTrace Box( BBox bbox, in Vector3 from, in Vector3 to )
	{
		return Ray( from, to ).Size( bbox );
	}

	/// <summary>
	/// Casts a box from a given position and direction, up to a given distance.
	/// </summary>
	public SceneTrace Box( BBox bbox, in Ray ray, in float distance )
	{
		return Ray( ray, distance ).Size( bbox );
	}

	/// <summary>
	/// Casts a capsule
	/// </summary>
	public readonly SceneTrace Capsule( Capsule capsule )
	{
		var t = this;
		t.PhysicsTrace = PhysicsTrace.Capsule( capsule );
		return t;
	}

	/// <summary>
	/// Casts a capsule from point A to point B.
	/// </summary>
	public SceneTrace Capsule( Capsule capsule, in Vector3 from, in Vector3 to )
	{
		var t = this;
		t.PhysicsTrace = PhysicsTrace.Capsule( capsule, from, to );
		return t;
	}

	/// <summary>
	/// Casts a capsule from a given position and direction, up to a given distance.
	/// </summary>
	public SceneTrace Capsule( Capsule capsule, in Ray ray, in float distance )
	{
		var t = this;
		t.PhysicsTrace = PhysicsTrace.Capsule( capsule, ray, distance );
		return t;
	}

	/// <summary>
	/// Casts a ray from point A to point B.
	/// </summary>
	public SceneTrace Ray( in Vector3 from, in Vector3 to )
	{
		var t = this;
		t.PhysicsTrace = PhysicsTrace.Ray( from, to );
		return t;
	}

	/// <summary>
	/// Casts a ray from a given position and direction, up to a given distance.
	/// </summary>
	public SceneTrace Ray( in Ray ray, in float distance )
	{
		var t = this;
		t.PhysicsTrace = PhysicsTrace.Ray( ray, distance );
		return t;
	}

	/// <summary>
	/// Casts a PhysicsBody from its current position and rotation to desired end point.
	/// </summary>
	public SceneTrace Body( PhysicsBody body, in Vector3 to )
	{
		var t = this;
		t.PhysicsTrace = PhysicsTrace.Body( body, to );
		return t;
	}

	/// <summary>
	/// Casts a PhysicsBody from a position and rotation to desired end point.
	/// </summary>
	public SceneTrace Body( PhysicsBody body, in Transform from, in Vector3 to )
	{
		var t = this;
		t.PhysicsTrace = PhysicsTrace.Body( body, from, to );
		return t;
	}

	/// <summary>
	/// Sweeps each <see cref="PhysicsShape">PhysicsShape</see> of given PhysicsBody and returns the closest collision. Does not support Mesh PhysicsShapes.
	/// Basically 'hull traces' but with physics shapes.
	/// Same as tracing a body but allows rotation to change during the sweep.
	/// </summary>
	public SceneTrace Sweep( in PhysicsBody body, in Transform from, in Transform to )
	{
		var t = this;
		t.PhysicsTrace = PhysicsTrace.Sweep( body, from, to );
		return t;
	}

	/// <summary>
	/// Creates a Trace.Sweep using the <see cref="PhysicsBody">PhysicsBody</see>'s position as the starting position.
	/// </summary>
	public SceneTrace Sweep( in PhysicsBody body, in Transform to )
	{
		return Sweep( body, body.Transform, to );
	}

	/// <summary>
	/// Sets the start and end positions of the trace request
	/// </summary>
	public readonly SceneTrace FromTo( in Vector3 from, in Vector3 to )
	{
		var t = this;
		t.PhysicsTrace = PhysicsTrace.FromTo( from, to );
		return t;
	}


	/// <summary>
	/// Makes this trace an axis aligned box of given size. Extracts mins and maxs from the Bounding Box.
	/// </summary>
	public readonly SceneTrace Size( in BBox hull )
	{
		return Size( hull.Mins, hull.Maxs );
	}

	/// <summary>
	/// Makes this trace an axis aligned box of given size. Calculates mins and maxs by assuming given size is (maxs-mins) and the center is in the middle.
	/// </summary>
	public readonly SceneTrace Size( in Vector3 size )
	{
		return Size( size * -0.5f, size * 0.5f );
	}

	/// <summary>
	/// Makes this trace an axis aligned box of given size.
	/// </summary>
	public readonly SceneTrace Size( in Vector3 mins, in Vector3 maxs )
	{
		var t = this;
		t.PhysicsTrace = PhysicsTrace.Size( mins, maxs );
		return t;
	}

	// Named this radius instead of size just incase there's some casting going on and Size gets called instead
	/// <summary>
	/// Makes this trace a sphere of given radius.
	/// </summary>
	public readonly SceneTrace Radius( float radius )
	{
		var t = this;
		t.PhysicsTrace = PhysicsTrace.Radius( radius );
		return t;
	}

	/// <summary>
	/// Should we hit hitboxes
	/// </summary>
	public readonly SceneTrace UseHitboxes( bool hit = true )
	{
		var t = this;
		t.IncludeHitboxes = hit;
		return t;
	}

	/// <summary>
	/// Only return entities with this tag. Subsequent calls to this will add multiple requirements
	/// and they'll all have to be met (ie, the entity will need all tags).
	/// </summary>
	public SceneTrace WithTag( string tag ) { var t = this; t.PhysicsTrace = t.PhysicsTrace.WithTag( tag ); return t; }

	/// <summary>
	/// Only return entities with all of these tags
	/// </summary>
	public SceneTrace WithAllTags( params string[] tags ) { var t = this; t.PhysicsTrace = t.PhysicsTrace.WithAllTags( tags ); return t; }

	/// <summary>
	/// Only return entities with all of these tags
	/// </summary>
	public SceneTrace WithAllTags( ITagSet tags ) { var t = this; t.PhysicsTrace = t.PhysicsTrace.WithAllTags( tags ); return t; }

	/// <summary>
	/// Only return entities with any of these tags
	/// </summary>
	public SceneTrace WithAnyTags( params string[] tags ) { var t = this; t.PhysicsTrace = t.PhysicsTrace.WithAllTags( tags ); return t; }

	/// <summary>
	/// Only return entities with any of these tags
	/// </summary>
	public SceneTrace WithAnyTags( ITagSet tags ) { var t = this; t.PhysicsTrace = t.PhysicsTrace.WithAnyTags( tags ); return t; }

	/// <summary>
	/// Only return entities without any of these tags
	/// </summary>
	public SceneTrace WithoutTags( params string[] tags ) { var t = this; t.PhysicsTrace = t.PhysicsTrace.WithoutTags( tags ); return t; }

	/// <summary>
	/// Only return entities without any of these tags
	/// </summary>
	public SceneTrace WithoutTags( ITagSet tags ) { var t = this; t.PhysicsTrace = t.PhysicsTrace.WithoutTags( tags ); return t; }

	/// <summary>
	/// Run the trace and return the result. The result will return the first hit.
	/// </summary>
	public readonly SceneTraceResult Run()
	{
		return scene.RunTrace( this );
	}
}

public struct SceneTraceResult
{
	public Scene Scene;
	/// <summary>
	/// Whether the trace hit something or not
	/// </summary>
	public bool Hit;

	/// <summary>
	/// Whether the trace started in a solid
	/// </summary>
	public bool StartedSolid;

	/// <summary>
	/// The start position of the trace
	/// </summary>
	public Vector3 StartPosition;

	/// <summary>
	/// The end or hit position of the trace
	/// </summary>
	public Vector3 EndPosition;

	/// <summary>
	/// The hit position of the trace
	/// </summary>
	public Vector3 HitPosition;

	/// <summary>
	/// The hit surface normal (direction vector)
	/// </summary>
	public Vector3 Normal;

	/// <summary>
	/// A fraction [0..1] of where the trace hit between the start and the original end positions
	/// </summary>
	public float Fraction;

	/// <summary>
	/// The GameObject that was hit
	/// </summary>
	public GameObject GameObject;

	/// <summary>
	/// The physics object that was hit, if any
	/// </summary>
	public PhysicsBody Body;

	/// <summary>
	/// The physics shape that was hit, if any
	/// </summary>
	public PhysicsShape Shape;

	/// <summary>
	/// The physical properties of the hit surface
	/// </summary>
	public Surface Surface;

	/// <summary>
	/// The id of the hit bone (either from hitbox or physics shape)
	/// </summary>
	public int Bone;

	/// <summary>
	/// The direction of the trace ray
	/// </summary>
	public Vector3 Direction;

	/// <summary>
	/// The triangle index hit, if we hit a mesh <see cref="PhysicsShape">physics shape</see>
	/// </summary>
	public int Triangle;

	/// <summary>
	/// The tags that the hit shape had
	/// </summary>
	public string[] Tags;

	/// <summary>
	/// The hitbox that we hit
	/// </summary>
	public Hitbox Hitbox;

	/// <summary>
	/// The distance between start and end positions.
	/// </summary>
	public float Distance => Vector3.DistanceBetween( StartPosition, EndPosition );

	public static SceneTraceResult From( in Scene scene, in PhysicsTraceResult physicsResult )
	{
		var result = new SceneTraceResult
		{
			Scene = scene,
			Hit = physicsResult.Hit,
			StartedSolid = physicsResult.StartedSolid,
			StartPosition = physicsResult.StartPosition,
			EndPosition = physicsResult.EndPosition,
			HitPosition = physicsResult.HitPosition,
			Normal = physicsResult.Normal,
			Fraction = physicsResult.Fraction,
			Body = physicsResult.Body,
			Shape = physicsResult.Shape,
			Surface = physicsResult.Surface,
			Bone = physicsResult.Bone,
			Direction = physicsResult.Direction,
			Triangle = physicsResult.Triangle,
			Tags = physicsResult.Tags,
			GameObject = (GameObject) physicsResult.Body?.GameObject
		};

		return result;
	}
}
