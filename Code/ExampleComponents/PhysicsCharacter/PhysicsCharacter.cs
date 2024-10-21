using Sandbox.PhysicsCharacterMode;


[Icon( "🕺" ), EditorHandle( Icon = "🕺" )]
[Alias( "PhysicalCharacterController" )]
public sealed partial class PhysicsCharacter : Component, IScenePhysicsEvents, Component.ExecuteInEditor
{
	/// <summary>
	/// This is used to keep a distance away from surfaces. For exmaple, when grounding, we'll
	/// be a skin distance away from the ground.
	/// </summary>
	const float _skin = 0.05f;

	[Property, Hide, RequireComponent] public Rigidbody Body { get; set; }

	public CapsuleCollider BodyCollider { get; private set; }
	public BoxCollider FeetCollider { get; private set; }

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

	/// <summary>
	/// True if we're using PhysicsCharacterLadderMode mode
	/// </summary>
	public bool IsClimbing => Mode is Sandbox.PhysicsCharacterMode.PhysicsCharacterLadderMode;

	/// <summary>
	/// True if we're currently using PhysicsCharacterSwimMode mode
	/// </summary>
	public bool IsSwimming => Mode is Sandbox.PhysicsCharacterMode.PhysicsCharacterSwimMode;

	protected override void OnAwake()
	{
		base.OnAwake();

		var walkMode = GetOrAddComponent<PhysicsCharacterWalkMode>();
		walkMode.Flags = walkMode.Flags.WithFlag( ComponentFlags.Hidden, true );

		Mode = GetComponent<PhysicsCharacterWalkMode>();

		EnsureComponentsCreated();
		UpdateBody();

		Body.Velocity = 0;
	}

	protected override void OnValidate()
	{
		EnsureComponentsCreated();
		UpdateBody();
	}

	void IScenePhysicsEvents.PrePhysicsStep()
	{
		ChooseBestMoveMode();
		UpdateBody();

		Mode.AddVelocity();

		TryStep();
	}

	void IScenePhysicsEvents.PostPhysicsStep()
	{
		RestoreStep();

		Reground();
		CategorizeGround();
		UpdateGroundVelocity();

		Velocity = Body.Velocity - GroundVelocity;

		Mode?.PostPhysicsStep();

		DebugDrawSystem.Current.Box( BodyBox(), transform: WorldTransform, color: Color.Green, duration: 0 );
		DebugDrawSystem.Current.Sphere( new Sphere( Body.MassCenterOverride, 2 ), transform: WorldTransform, color: Color.Green, duration: 0 );

		//DebugDrawSystem.Current.Sphere( new Sphere( WorldPosition + Vector3.Up * 100, 10 ), color: Color.Green );
		//DebugDrawSystem.Current.Text( WorldPosition + Vector3.Up * 100, "Hello!", duration: 0 );

		ChooseBestMoveMode();
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
		PreventGrounding( 0.2f );

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
