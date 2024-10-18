public sealed partial class PhysicalCharacterController : Component, IScenePhysicsEvents
{
	[Property, Group( "Body" ), RequireComponent] public Rigidbody Body { get; set; }
	[Property, Group( "Body" )] public float BodyRadius { get; set; } = 16.0f;
	[Property, Group( "Body" )] public float BodyHeight { get; set; } = 64.0f;

	[Property, Group( "Ground" )] public float GroundAngle { get; set; } = 45.0f;


	public Vector3 WishVelocity { get; set; }
	public bool IsOnGround => GroundObject.IsValid();



	public Vector3 Velocity { get; private set; }
	public Vector3 GroundVelocity { get; set; }
	public float GroundYaw { get; set; }


	protected override void OnAwake()
	{
		base.OnAwake();

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
	}

	void UpdateBody()
	{
		var feetHeight = StepHeight;

		var bodyCollider = Body.GameObject.GetOrAddComponent<CapsuleCollider>();
		bodyCollider.Radius = BodyRadius;
		bodyCollider.Start = Vector3.Up * (BodyHeight - bodyCollider.Radius);
		bodyCollider.End = Vector3.Up * (bodyCollider.Radius + feetHeight - bodyCollider.Radius * 0.20f);
		bodyCollider.Friction = 0.0f;

		/*
		var feetCollider = Body.GameObject.GetOrAddComponent<SphereCollider>();
		feetCollider.Radius = BodyRadius * 0.5f;
		feetCollider.Center = new Vector3( 0, 0, BodyRadius * 0.5f );
		feetCollider.Friction = IsOnGround ? 2340.5f : 0;
		*/



		var feetCollider = Body.GameObject.GetOrAddComponent<BoxCollider>();
		feetCollider.Scale = new Vector3( BodyRadius, BodyRadius, feetHeight );
		feetCollider.Center = new Vector3( 0, 0, feetHeight * 0.5f );
		feetCollider.Friction = IsOnGround ? 10f : 0;


		float massCenter = 0;// WishVelocity.Length.Clamp( 0, StepHeight );

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
