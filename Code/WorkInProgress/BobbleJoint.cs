using Sandbox.Physics;

/// <summary>
/// A joint which creates a "bobble head" type movement
/// </summary>
public sealed class BobbleJoint : Joint
{
	private Sandbox.Physics.BallSocketJoint ballJoint;
	private Sandbox.Physics.SpringJoint springJoint;

	private float springDistance;

	[Property, MakeDirty, Range( 0, 10 )]
	public float Springiness { get; set; } = 1.0f;

	[Property, MakeDirty, Range( 0, 50 )]
	public float SpringFrequency { get; set; } = 5.0f;

	[Property, MakeDirty, Range( 0, 5 )]
	public float Damping { get; set; } = 1.0f;

	protected override PhysicsJoint CreateJoint( PhysicsPoint point1, PhysicsPoint point2 )
	{
		var originPoint = WorldPosition;
		var springDir = (point1.Body.Transform.Position - originPoint);

		if ( springDir.Length < 10.0f ) springDir = springDir.Normal * 10.0f;
		if ( springDir.Length > 100.0f ) springDir = springDir.Normal * 100.0f;

		// Join them at the origin point
		point1.LocalRotation = point1.Body.Transform.RotationToLocal( Rotation.LookAt( springDir ) );
		point2.LocalRotation = point2.Body.Transform.RotationToLocal( Rotation.LookAt( springDir ) );

		point1.LocalPosition = point1.Body.Transform.PointToLocal( originPoint );
		point2.LocalPosition = point2.Body.Transform.PointToLocal( originPoint );

		ballJoint = PhysicsJoint.CreateBallSocket( point1, point2 );

		point1.LocalPosition = point1.Body.Transform.PointToLocal( originPoint + springDir * 0.5f );
		point2.LocalPosition = point2.Body.Transform.PointToLocal( originPoint + springDir * -0.5f );

		springDistance = point1.Transform.Position.Distance( point2.Transform.Position );

		springJoint = PhysicsJoint.CreateSpring( point1, point2, springDistance, springDistance );
		springJoint.SpringLinear = new PhysicsSpring { Damping = 0, Frequency = 3 };

		UpdateBallJoint();
		UpdateSpringJoint();

		return ballJoint;
	}

	void UpdateBallJoint()
	{
		if ( !ballJoint.IsValid() )
			return;

		ballJoint.Friction = Damping;
		ballJoint.SwingLimitEnabled = true;
		ballJoint.SwingLimit = 80;
		ballJoint.TwistLimitEnabled = false;
		ballJoint.TwistLimit = new Vector2( -10, 10 );
	}

	void UpdateSpringJoint()
	{
		if ( !springJoint.IsValid() )
			return;

		springJoint.SpringLinear = new PhysicsSpring { Damping = Damping, Frequency = Springiness };
		springJoint.MinLength = springDistance + springDistance * Springiness;
		springJoint.MaxLength = springDistance + springDistance * Springiness;
	}

	protected override void DestroyJoint()
	{
		base.DestroyJoint();

		springJoint?.Remove();
		springJoint = null;
	}

	protected override void OnDirty()
	{
		base.OnDirty();

		UpdateBallJoint();
		UpdateSpringJoint();
	}
}
