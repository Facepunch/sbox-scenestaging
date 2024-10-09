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

		if ( !WishVelocity.IsNearZeroLength )
		{
			var z = Body.Velocity.z;

			var velocity = (Body.Velocity - GroundVelocity).WithZ( 0 );
			var wish = WishVelocity.WithZ( 0 );
			var speed = velocity.Length;

			var maxSpeed = MathF.Max( wish.Length, speed );

			if ( IsOnGround )
			{
				var amount = 15;
				velocity = velocity.AddClamped( wish * amount, wish.Length * amount );
			}
			else
			{
				var amount = 0.15f;
				velocity = velocity.AddClamped( wish * amount, wish.Length );
			}

			if ( velocity.Length > maxSpeed )
				velocity = velocity.Normal * maxSpeed;

			velocity += GroundVelocity;
			velocity.z = z;

			Body.Velocity = velocity;
		}

		//TryStep();
	}

	void IScenePhysicsEvents.PostPhysicsStep()
	{
		CategorizeGround();
		UpdateGroundVelocity();

		Velocity = Body.Velocity - GroundVelocity;
	}

	protected override void OnUpdate()
	{
		Gizmo.Draw.ScreenText( $"Velocity: {Body.Velocity.Length:0.00}\n" +
			$"Velocity XY: {Body.Velocity.WithZ( 0 ).Length:0.00}", 100 );
	}

	void UpdateBody()
	{
		var bodyCollider = Body.GameObject.GetOrAddComponent<CapsuleCollider>();
		bodyCollider.Radius = BodyRadius;
		bodyCollider.Start = Vector3.Up * (BodyHeight - bodyCollider.Radius);
		bodyCollider.End = Vector3.Up * (bodyCollider.Radius + StepHeight * 0.1f);
		bodyCollider.Friction = 0.0f;

		var feetCollider = Body.GameObject.GetOrAddComponent<SphereCollider>();
		feetCollider.Radius = BodyRadius * 0.5f;
		feetCollider.Center = new Vector3( 0, 0, BodyRadius * 0.5f );
		feetCollider.Friction = IsOnGround ? 1.5f : 0;


		float massCenter = WishVelocity.Length.Clamp( 0, StepHeight + 2 );

		if ( !IsOnGround )
			massCenter = BodyHeight * 0.5f;

		Body.OverrideMassCenter = true;
		Body.MassCenterOverride = new Vector3( 0, 0, massCenter );
	}

	Transform _groundTransform;
	Transform _groundLocal;

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

	public void Punch( Vector3 velocity )
	{
		PreventGroundingForSeconds( 0.5f );
		UpdateBody();
		Body.PhysicsBody.ApplyForce( velocity * Body.PhysicsBody.Mass * 300 );
	}
}
