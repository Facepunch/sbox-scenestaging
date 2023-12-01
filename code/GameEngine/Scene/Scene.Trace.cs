using Sandbox;

public partial class Scene : GameObject
{
	public SceneTraceBuilder Trace => new SceneTraceBuilder( this );

}


public partial struct SceneTraceBuilder
{
	Scene scene;
	PhysicsTraceBuilder physicsTrace;
	bool hitboxes;

	internal SceneTraceBuilder( Scene scene )
	{
		this.scene = scene;
		physicsTrace = GameManager.ActiveScene.PhysicsWorld.Trace;
	}

	/// <summary>
	/// Casts a sphere from point A to point B.
	/// </summary>
	public SceneTraceBuilder Sphere( float radius, in Vector3 from, in Vector3 to ) => Ray( from, to ).Radius( radius );

	/// <summary>
	/// Casts a sphere from a given position and direction, up to a given distance.
	/// </summary>
	public SceneTraceBuilder Sphere( float radius, in Ray ray, in float distance ) => Ray( ray, distance ).Radius( radius );

	/// <summary>
	/// Casts a box from point A to point B.
	/// </summary>
	public SceneTraceBuilder Box( Vector3 extents, in Vector3 from, in Vector3 to )
	{
		return Ray( from, to ).Size( extents );
	}

	/// <summary>
	/// Casts a box from a given position and direction, up to a given distance.
	/// </summary>
	public SceneTraceBuilder Box( Vector3 extents, in Ray ray, in float distance )
	{
		return Ray( ray, distance ).Size( extents );
	}

	/// <summary>
	/// Casts a box from point A to point B.
	/// </summary>
	public SceneTraceBuilder Box( BBox bbox, in Vector3 from, in Vector3 to )
	{
		return Ray( from, to ).Size( bbox );
	}

	/// <summary>
	/// Casts a box from a given position and direction, up to a given distance.
	/// </summary>
	public SceneTraceBuilder Box( BBox bbox, in Ray ray, in float distance )
	{
		return Ray( ray, distance ).Size( bbox );
	}

	/// <summary>
	/// Casts a capsule
	/// </summary>
	public readonly SceneTraceBuilder Capsule( Capsule capsule )
	{
		var t = this;
		t.physicsTrace = physicsTrace.Capsule( capsule );
		return t;
	}

	/// <summary>
	/// Casts a capsule from point A to point B.
	/// </summary>
	public SceneTraceBuilder Capsule( Capsule capsule, in Vector3 from, in Vector3 to )
	{
		var t = this;
		t.physicsTrace = physicsTrace.Capsule( capsule, from, to );
		return t;
	}

	/// <summary>
	/// Casts a capsule from a given position and direction, up to a given distance.
	/// </summary>
	public SceneTraceBuilder Capsule( Capsule capsule, in Ray ray, in float distance )
	{
		var t = this;
		t.physicsTrace = physicsTrace.Capsule( capsule, ray, distance );
		return t;
	}

	/// <summary>
	/// Casts a ray from point A to point B.
	/// </summary>
	public SceneTraceBuilder Ray( in Vector3 from, in Vector3 to )
	{
		var t = this;
		t.physicsTrace = physicsTrace.Ray( from, to );
		return t;
	}

	/// <summary>
	/// Casts a ray from a given position and direction, up to a given distance.
	/// </summary>
	public SceneTraceBuilder Ray( in Ray ray, in float distance )
	{
		var t = this;
		t.physicsTrace = physicsTrace.Ray( ray, distance );
		return t;
	}

	/// <summary>
	/// Casts a PhysicsBody from its current position and rotation to desired end point.
	/// </summary>
	public SceneTraceBuilder Body( PhysicsBody body, in Vector3 to )
	{
		var t = this;
		t.physicsTrace = physicsTrace.Body( body, to );
		return t;
	}

	/// <summary>
	/// Casts a PhysicsBody from a position and rotation to desired end point.
	/// </summary>
	public SceneTraceBuilder Body( PhysicsBody body, in Transform from, in Vector3 to )
	{
		var t = this;
		t.physicsTrace = physicsTrace.Body( body, from, to );
		return t;
	}

	/// <summary>
	/// Sweeps each <see cref="PhysicsShape">PhysicsShape</see> of given PhysicsBody and returns the closest collision. Does not support Mesh PhysicsShapes.
	/// Basically 'hull traces' but with physics shapes.
	/// Same as tracing a body but allows rotation to change during the sweep.
	/// </summary>
	public SceneTraceBuilder Sweep( in PhysicsBody body, in Transform from, in Transform to )
	{
		var t = this;
		t.physicsTrace = physicsTrace.Sweep( body, from, to );
		return t;
	}

	/// <summary>
	/// Creates a Trace.Sweep using the <see cref="PhysicsBody">PhysicsBody</see>'s position as the starting position.
	/// </summary>
	public SceneTraceBuilder Sweep( in PhysicsBody body, in Transform to )
	{
		return Sweep( body, body.Transform, to );
	}

	/// <summary>
	/// Sets the start and end positions of the trace request
	/// </summary>
	public readonly SceneTraceBuilder FromTo( in Vector3 from, in Vector3 to )
	{
		var t = this;
		t.physicsTrace = physicsTrace.FromTo( from, to );
		return t;
	}


	/// <summary>
	/// Makes this trace an axis aligned box of given size. Extracts mins and maxs from the Bounding Box.
	/// </summary>
	public readonly SceneTraceBuilder Size( in BBox hull )
	{
		return Size( hull.Mins, hull.Maxs );
	}

	/// <summary>
	/// Makes this trace an axis aligned box of given size. Calculates mins and maxs by assuming given size is (maxs-mins) and the center is in the middle.
	/// </summary>
	public readonly SceneTraceBuilder Size( in Vector3 size )
	{
		return Size( size * -0.5f, size * 0.5f );
	}

	/// <summary>
	/// Makes this trace an axis aligned box of given size.
	/// </summary>
	public readonly SceneTraceBuilder Size( in Vector3 mins, in Vector3 maxs )
	{
		var t = this;
		t.physicsTrace = physicsTrace.Size( mins, maxs );
		return t;
	}

	// Named this radius instead of size just incase there's some casting going on and Size gets called instead
	/// <summary>
	/// Makes this trace a sphere of given radius.
	/// </summary>
	public readonly SceneTraceBuilder Radius( float radius )
	{
		var t = this;
		t.physicsTrace = physicsTrace.Radius( radius );
		return t;
	}

	/// <summary>
	/// Should we hit hitboxes
	/// </summary>
	public readonly SceneTraceBuilder UseHitboxes( bool hit = true )
	{
		var t = this;
		t.hitboxes = hit;
		return t;
	}

	/// <summary>
	/// Only return entities with this tag. Subsequent calls to this will add multiple requirements
	/// and they'll all have to be met (ie, the entity will need all tags).
	/// </summary>
	public SceneTraceBuilder WithTag( string tag ) { var t = this; t.physicsTrace = t.physicsTrace.WithTag( tag ); return t; }

	/// <summary>
	/// Only return entities with all of these tags
	/// </summary>
	public SceneTraceBuilder WithAllTags( params string[] tags ) { var t = this; t.physicsTrace = t.physicsTrace.WithAllTags( tags ); return t; }

	/// <summary>
	/// Only return entities with all of these tags
	/// </summary>
	public SceneTraceBuilder WithAllTags( ITagSet tags ) { var t = this; t.physicsTrace = t.physicsTrace.WithAllTags( tags ); return t; }

	/// <summary>
	/// Only return entities with any of these tags
	/// </summary>
	public SceneTraceBuilder WithAnyTags( params string[] tags ) { var t = this; t.physicsTrace = t.physicsTrace.WithAllTags( tags ); return t; }

	/// <summary>
	/// Only return entities with any of these tags
	/// </summary>
	public SceneTraceBuilder WithAnyTags( ITagSet tags ) { var t = this; t.physicsTrace = t.physicsTrace.WithAnyTags( tags ); return t; }

	/// <summary>
	/// Only return entities without any of these tags
	/// </summary>
	public SceneTraceBuilder WithoutTags( params string[] tags ) { var t = this; t.physicsTrace = t.physicsTrace.WithoutTags( tags ); return t; }

	/// <summary>
	/// Only return entities without any of these tags
	/// </summary>
	public SceneTraceBuilder WithoutTags( ITagSet tags ) { var t = this; t.physicsTrace = t.physicsTrace.WithoutTags( tags ); return t; }

	/// <summary>
	/// Run the trace and return the result. The result will return the first hit.
	/// </summary>
	public readonly SceneTraceResult Run()
	{
		var physicsResult = physicsTrace.Run();

		SceneTraceResult sceneResult = SceneTraceResult.From( scene, physicsResult );

		if ( hitboxes )
		{
			// pool me
			List<SceneTraceResult> results = new List<SceneTraceResult>();

			// collect hits from all hitbox groups
			var groups = scene.GetAllComponents<HitboxGroup>();
			foreach ( var group in groups )
			{
				group.Trace( this, results );
			}

			// order results by closest
			foreach( var result in results.OrderBy( x => x.Fraction ) )
			{
				// further away than our physics result, which means all the 
				// others are too, so just bail
				if ( result.Fraction > physicsResult.Fraction )
					break;

				return result;
			}
		}

		return sceneResult;
	}

	/// <summary>
	/// Run the trace and return the result. The result will return the first hit.
	/// </summary>
	public readonly SceneTraceResult RunAgainstCapsule( in Capsule capsule, in Transform transform )
	{
		return SceneTraceResult.From( scene, physicsTrace.RunAgainstCapsule( capsule, transform ) );
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

	internal static SceneTraceResult From( in Scene scene, in PhysicsTraceResult physicsResult )
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
