using Sandbox;

public sealed class BrickBall : BaseComponent, BaseComponent.ICollisionListener
{
	[Property] public GameObject CollisionIndicator { get; set; }

	[Property] public Vector3 Direction { get; set; } = Vector3.Up + Vector3.Right;

	[Range( 0, 100 )]
	[Property] public float Speed { get; set; } = 100.0f;

	public override void DrawGizmos()
	{
		Gizmo.Draw.Line( 0, Direction.Normal * 100.0f );
	}

	float StartX;

	public override void OnEnabled()
	{
		base.OnEnabled();

		StartX = Transform.Position.x;

		var rigidBody = GetComponent<PhysicsComponent>();
		rigidBody.Velocity = (Direction.Normal * Speed).WithX( 0 );
	}

	public override void FixedUpdate()
	{


		Transform.LocalRotation = Rotation.Identity;
		Transform.Position = Transform.Position.WithX( StartX );
	}

	public void OnCollisionStart( Collision o )
	{
		Direction = Vector3.Reflect( Direction, o.Contact.Normal ).Normal.WithX( 0 );
		o.Self.Body.Velocity = Direction * Speed;
		o.Self.Body.AngularVelocity = 0;

		if ( CollisionIndicator is not null )
		{
			SceneUtility.Instantiate( CollisionIndicator, o.Contact.Point );
		}
	}

	public void OnCollisionStop( CollisionStop o )
	{
		o.Self.Body.Velocity = Direction * Speed;
		o.Self.Body.AngularVelocity = 0;
	}

	public void OnCollisionUpdate( Collision o )
	{
		Direction = Vector3.Reflect( Direction, o.Contact.Normal ).Normal.WithX( 0 );
		o.Self.Body.Velocity = Direction * Speed;
		o.Self.Body.AngularVelocity = 0;

		if ( CollisionIndicator is not null )
		{
			SceneUtility.Instantiate( CollisionIndicator, o.Contact.Point );
		}
	}
}
