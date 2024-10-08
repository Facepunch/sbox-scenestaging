public sealed partial class PhysicalCharacterController : Component
{
	[Property, Group( "Body" ), RequireComponent] public Rigidbody Body { get; set; }
	[Property, Group( "Body" )] public float BodyRadius { get; set; } = 16.0f;
	[Property, Group( "Body" )] public float BodyHeight { get; set; } = 64.0f;

	[Property, Group( "Ground" )] public Surface BodySurfaceGrounded { get; set; }
	[Property, Group( "Ground" )] public Surface BodySurfaceAir { get; set; }
	[Property, Group( "Ground" )] public float GroundAngle { get; set; } = 45.0f;


	public Vector3 WishVelocity { get; set; }
	public bool IsOnGround => GroundObject.IsValid();



	public Vector3 Velocity => Body.Velocity - GroundVelocity;
	public Vector3 GroundVelocity { get; set; }
	public float GroundYaw { get; set; }
	GameObject GroundObject { get; set; }

	protected override void OnAwake()
	{
		base.OnAwake();

		UpdateBody();
	}

	protected override void OnFixedUpdate()
	{
		UpdateBody();
		UpdateGroundVelocity();

		if ( !WishVelocity.IsNearZeroLength )
		{
			var z = Body.Velocity.z;

			var velocity = (Body.Velocity - GroundVelocity).WithZ( 0 );
			var wish = WishVelocity.WithZ( 0 );
			var speed = velocity.Length;

			var maxSpeed = MathF.Max( wish.Length, speed );
			var amount = IsOnGround ? 1 : 0.1f;
			velocity = velocity.AddClamped( wish * amount, wish.Length * amount );

			if ( velocity.Length > maxSpeed )
				velocity = velocity.Normal * maxSpeed;

			if ( IsOnGround )
			{
				velocity = WishVelocity.Normal * velocity.Length;
			}

			velocity += GroundVelocity;
			velocity.z = z;

			Body.Velocity = velocity;
		}

		TryStep();
		CategorizeGround();
	}

	void UpdateBody()
	{
		var bodyCollider = Body.GameObject.GetOrAddComponent<CapsuleCollider>();
		bodyCollider.Radius = BodyRadius * 0.8f;
		bodyCollider.Start = Vector3.Up * (BodyHeight - bodyCollider.Radius);
		bodyCollider.End = Vector3.Up * (bodyCollider.Radius + StepHeight * 0.5f);
		bodyCollider.Surface = BodySurfaceAir;

		var feetCollider = Body.GameObject.GetOrAddComponent<BoxCollider>();
		feetCollider.Scale = new Vector3( BodyRadius, BodyRadius, BodyHeight * 0.8f );
		feetCollider.Center = new Vector3( 0, 0, BodyHeight * 0.4f );
		feetCollider.Surface = IsOnGround ? BodySurfaceGrounded : BodySurfaceAir;
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

		var worldPos = WorldPosition;

		var posLocal = _groundTransform.PointToLocal( worldPos );
		var newWorldPos = GroundObject.WorldTransform.PointToWorld( posLocal );
		var posChange = newWorldPos - worldPos;

		GroundVelocity = posChange * (1 / Time.Delta);
	}

	public void Punch( Vector3 velocity )
	{
		Body.PhysicsBody.ApplyForce( velocity * Body.PhysicsBody.Mass * 300 );
		PreventGroundingForSeconds( 0.5f );
	}
}
