[Icon( "🕺" ), EditorHandle( Icon = "🕺" )]
public sealed partial class PhysicalCharacterController : Component, IScenePhysicsEvents, Component.ExecuteInEditor
{
	[Property, Hide, RequireComponent] public Rigidbody Body { get; set; }

	CapsuleCollider BodyCollider { get; set; }
	BoxCollider FeetCollider { get; set; }

	bool _showRigidBodyComponent;


	[Property, Group( "Body" )] public float BodyRadius { get; set; } = 16.0f;
	[Property, Group( "Body" )] public float BodyHeight { get; set; } = 64.0f;
	[Property, Group( "Body" )] public float BodyMass { get; set; } = 500;

	[Property, Group( "Body" ), Title( "Show Rigidbody" )]
	public bool ShowRigidbodyComponent
	{
		get => _showRigidBodyComponent;
		set
		{
			_showRigidBodyComponent = value;

			if ( Body.IsValid() )
			{
				Body.Flags = Body.Flags.WithFlag( ComponentFlags.Hidden, !value );
			}
		}
	}

	bool _showColliderComponent;

	[Property, Group( "Body" ), Title( "Show Colliders" )]
	public bool ShowColliderComponents
	{
		get => _showColliderComponent;
		set
		{
			_showColliderComponent = value;

			if ( BodyCollider.IsValid() )
			{
				BodyCollider.Flags = BodyCollider.Flags.WithFlag( ComponentFlags.Hidden, !value );
			}

			if ( FeetCollider.IsValid() )
			{
				FeetCollider.Flags = FeetCollider.Flags.WithFlag( ComponentFlags.Hidden, !value );
			}
		}
	}



	[Property, Group( "Ground" )] public float GroundAngle { get; set; } = 45.0f;


	public Vector3 WishVelocity { get; set; }
	public bool IsOnGround => GroundObject.IsValid();

	public Vector3 Velocity { get; private set; }
	public Vector3 GroundVelocity { get; set; }
	public float GroundYaw { get; set; }

	protected override void OnAwake()
	{
		base.OnAwake();

		EnsureComponentsCreated();
		UpdateBody();

		Body.Velocity = 0;
	}

	void EnsureComponentsCreated()
	{
		Body.CollisionEventsEnabled = true;
		Body.CollisionUpdateEventsEnabled = true;

		BodyCollider = Body.GameObject.GetOrAddComponent<CapsuleCollider>();
		FeetCollider = Body.GameObject.GetOrAddComponent<BoxCollider>();

		Body.Flags = Body.Flags.WithFlag( ComponentFlags.Hidden, !_showRigidBodyComponent );
		BodyCollider.Flags = BodyCollider.Flags.WithFlag( ComponentFlags.Hidden, !_showColliderComponent );
		FeetCollider.Flags = FeetCollider.Flags.WithFlag( ComponentFlags.Hidden, !_showColliderComponent );
	}

	protected override void OnValidate()
	{
		EnsureComponentsCreated();
		UpdateBody();
	}

	void IScenePhysicsEvents.PrePhysicsStep()
	{
		UpdateBody();

		var groundFriction = 0.25f + GroundFriction * 10;

		if ( !WishVelocity.IsNearZeroLength )
		{
			var z = Body.Velocity.z;

			var velocity = (Body.Velocity - GroundVelocity);
			var wish = WishVelocity;
			var speed = velocity.Length;

			var maxSpeed = MathF.Max( wish.Length, speed );

			if ( IsOnGround )
			{
				var amount = 1 * groundFriction;
				velocity = velocity.AddClamped( wish * amount, wish.Length * amount );
			}
			else
			{
				var amount = 0.05f;
				velocity = velocity.AddClamped( wish * amount, wish.Length );
			}

			if ( velocity.Length > maxSpeed )
				velocity = velocity.Normal * maxSpeed;

			velocity += GroundVelocity;

			if ( IsOnGround )
			{
				velocity.z = z;
			}

			Body.Velocity = velocity;
		}

		TryStep();
	}

	void IScenePhysicsEvents.PostPhysicsStep()
	{
		RestoreStep();

		CategorizeGround();
		CategorizeTriggers();
		UpdatePositionOnLadder();
		UpdateGroundVelocity();

		Velocity = Body.Velocity - GroundVelocity;

		DebugDrawSystem.Current.Box( BodyBox(), transform: WorldTransform, color: Color.Green, duration: 0 );

		DebugDrawSystem.Current.Sphere( new Sphere( WorldPosition + Vector3.Up * 100, 10 ), color: Color.Green );
		DebugDrawSystem.Current.Text( WorldPosition + Vector3.Up * 100, "Hello!", duration: 0 );
	}

	void UpdateBody()
	{
		var feetHeight = StepHeight;
		var radius = (BodyRadius * MathF.Sqrt( 2 )) / 2;

		BodyCollider.Radius = radius;
		BodyCollider.Start = Vector3.Up * (BodyHeight - BodyCollider.Radius);
		BodyCollider.End = Vector3.Up * (BodyCollider.Radius + feetHeight - BodyCollider.Radius * 0.20f);
		BodyCollider.Friction = 0.0f;
		BodyCollider.Enabled = true;

		/*
		var feetCollider = Body.GameObject.GetOrAddComponent<SphereCollider>();
		feetCollider.Radius = BodyRadius * 0.5f;
		feetCollider.Center = new Vector3( 0, 0, BodyRadius * 0.5f );
		feetCollider.Friction = IsOnGround ? 2340.5f : 0;
		*/


		FeetCollider.Scale = new Vector3( BodyRadius, BodyRadius, feetHeight );
		FeetCollider.Center = new Vector3( 0, 0, feetHeight * 0.5f );
		FeetCollider.Friction = IsOnGround ? 10f : 0;
		FeetCollider.Enabled = true;


		float massCenter = WishVelocity.Length.Clamp( 0, StepHeight );

		if ( !IsOnGround )
			massCenter = BodyHeight * 0.5f;


		if ( IsOnGround )
		{
			//Body.Locking = new PhysicsLock { Pitch = true, Yaw = true, Roll = true };
			Body.OverrideMassCenter = true;
			Body.MassCenterOverride = new Vector3( 0, 0, massCenter );
		}
		else
		{
			Body.OverrideMassCenter = false;
			//	Body.Locking = default;
		}

		Body.Locking = new PhysicsLock { Pitch = true, Yaw = true, Roll = true };
		Body.RigidbodyFlags = RigidbodyFlags.DisableCollisionSounds;

		Body.MassOverride = BodyMass;
	}

	Transform _groundTransform;

	void UpdateGroundVelocity()
	{
		if ( GroundObject is null )
		{
			GroundVelocity = 0;
			return;
		}

		if ( GroundComponent is Collider collider )
		{
			GroundVelocity = collider.GetVelocityAtPoint( WorldPosition );
		}

		if ( GroundComponent is Rigidbody rigidbody )
		{
			GroundVelocity = rigidbody.GetVelocityAtPoint( WorldPosition );
		}
	}

	/// <summary>
	/// Adds velocity in a special way. First we subtract any opposite velocity (ie, falling) then 
	/// we add the velocity, but we clamp it to that direction. This means that if you jump when you're running
	/// up a platform, you don't get extra jump power.
	/// </summary>
	public void Jump( Vector3 velocity )
	{
		PreventGroundingForSeconds( 0.2f );
		UpdateBody();

		var currentVel = Body.Velocity;

		// moving in the opposite direction
		// because this is a jump, we want to counteract that
		var dot = currentVel.Dot( velocity );
		if ( dot < 0 )
		{
			currentVel = currentVel.SubtractDirection( velocity.Normal, 1 );
		}

		currentVel = currentVel.AddClamped( velocity, velocity.Length );

		Body.Velocity = currentVel;
	}

}
