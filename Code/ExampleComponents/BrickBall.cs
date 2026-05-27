using Sandbox;

public sealed class BrickBall : Component, Component.ICollisionListener
{
	[Property] public GameObject CollisionIndicator { get; set; }

	[Property] public Vector3 Direction { get; set; } = Vector3.Up + Vector3.Right;

	[Range( 0, 100 )]
	[Property] public float Speed { get; set; } = 100.0f;

	/// <summary>
	/// Normal of the plane the ball is locked to. Default is forward (XY plane).
	/// </summary>
	[Property] public Vector3 PlaneNormal { get; set; } = Vector3.Forward;

	Vector3 Flatten( Vector3 v ) => v - Vector3.Dot( v, PlaneNormal ) * PlaneNormal;

	protected override void DrawGizmos()
	{
		Gizmo.Draw.Line( 0, Direction.Normal * 100.0f );
	}

	float StartDepth;

	protected override void OnStart()
	{
		base.OnStart();

		StartDepth = Vector3.Dot( WorldPosition, PlaneNormal );

		var rigidBody = Components.Get<Rigidbody>();
		rigidBody.Velocity = Flatten( Direction.Normal * Speed );
	}

	protected override void OnFixedUpdate()
	{
		LocalRotation = Rotation.Identity;
		WorldPosition = Flatten( WorldPosition ) + PlaneNormal * StartDepth;
	}

	public void OnCollisionStart( Collision o )
	{
		Direction = Flatten( Vector3.Reflect( Direction.Normal, o.Contact.Normal ) ).Normal;
		o.Self.Body.Velocity = Direction * Speed;
		o.Self.Body.AngularVelocity = 0;

		if ( CollisionIndicator.IsValid() )
		{
			CollisionIndicator.Clone( o.Contact.Point );
		}
	}

	public void OnCollisionStop( CollisionStop o )
	{
		o.Self.Body.Velocity = Direction * Speed;
		o.Self.Body.AngularVelocity = 0;
	}

	public void OnCollisionUpdate( Collision o )
	{
		Direction = Flatten( Vector3.Reflect( Direction, o.Contact.Normal ) ).Normal;
		o.Self.Body.Velocity = Direction * Speed;
		o.Self.Body.AngularVelocity = 0;

		if ( CollisionIndicator.IsValid() )
		{
			CollisionIndicator.Clone( o.Contact.Point );
		}
	}
}
