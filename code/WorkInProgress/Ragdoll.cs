namespace Sandbox;

/// <summary>
/// Work in progress ragdoll component that creates child rigid bodies from model physics.
/// </summary>
public sealed class Ragdoll : Component, Component.ExecuteInEditor
{
	private Model _model;
	private SkinnedModelRenderer _renderer;
	private RigidbodyFlags _rigidBodyFlags;
	private PhysicsLock _locking;
	private bool _motionEnabled = true;
	private Rigidbody _rootBody;

	private record BodyRecord( Rigidbody Component, BoneCollection.Bone Bone );
	private readonly List<BodyRecord> _bodies = new();

	private record JointRecord( Joint Component, Transform Frame1, Transform Frame2 );
	private readonly List<JointRecord> _joints = new();

	private readonly List<Collider> _colliders = new();

	[Property]
	public Model Model
	{
		get => _model;
		set
		{
			if ( _model == value )
				return;

			_model = value;

			CreatePhysics();
		}
	}

	[Property]
	public SkinnedModelRenderer Renderer
	{
		get => _renderer;
		set
		{
			if ( _renderer == value )
				return;

			if ( _renderer.IsValid() )
			{
				_renderer.ClearPhysicsBones();
			}

			_renderer = value;
		}
	}

	[Property]
	public bool IgnoreRoot { get; set; }

	[Property, Group( "Physics" )]
	public RigidbodyFlags RigidbodyFlags
	{
		get => _rigidBodyFlags;
		set
		{
			if ( _rigidBodyFlags == value )
				return;

			_rigidBodyFlags = value;

			foreach ( var body in _bodies )
			{
				if ( !body.Component.IsValid() )
					continue;

				body.Component.RigidbodyFlags = value;
			}
		}
	}

	[Property, Group( "Physics" )]
	public PhysicsLock Locking
	{
		get => _locking;
		set
		{
			_locking = value;

			foreach ( var body in _bodies )
			{
				if ( !body.Component.IsValid() )
					continue;

				body.Component.Locking = value;
			}
		}
	}

	/// <summary>
	/// Enable to drive renderer from physics, disable to drive physics from renderer.
	/// </summary>
	[Property, Group( "Physics" )]
	public bool MotionEnabled
	{
		get => _motionEnabled;
		set
		{
			if ( _motionEnabled == value )
				return;

			_motionEnabled = value;

			foreach ( var body in _bodies )
			{
				if ( !body.Component.IsValid() )
					continue;

				body.Component.MotionEnabled = value;
			}
		}
	}

	private void CreatePhysics()
	{
		if ( !Active )
			return;

		DestroyPhysics();

		if ( !Model.IsValid() )
			return;

		var physics = Model.Physics;
		if ( physics is null )
			return;

		if ( physics.Parts.Count == 0 )
			return;

		var world = WorldTransform;

		foreach ( var part in physics.Parts )
		{
			var bone = Model.Bones.GetBone( part.BoneName );

			if ( !Renderer.IsValid() || !Renderer.TryGetBoneTransform( bone, out var boneWorld ) )
			{
				// There's no renderer bones, use physics bind pose
				boneWorld = world.ToWorld( part.Transform );
			}

			var go = GameObject.Children.FirstOrDefault( x => x.Name == part.BoneName );
			go ??= Scene.CreateObject();
			go.Flags |= GameObjectFlags.Absolute | GameObjectFlags.Bone;
			go.Name = part.BoneName;
			go.WorldTransform = boneWorld;
			go.Parent = GameObject;

			var body = go.AddComponent<Rigidbody>( false );
			body.Flags |= ComponentFlags.NotSaved;
			body.RigidbodyFlags = RigidbodyFlags;
			body.Locking = Locking;
			body.MotionEnabled = MotionEnabled;
			body.LinearDamping = part.LinearDamping;
			body.AngularDamping = part.AngularDamping;

			_bodies.Add( new BodyRecord( body, bone ) );

			foreach ( var sphere in part.Spheres )
			{
				var collider = go.AddComponent<SphereCollider>();
				collider.Flags |= ComponentFlags.NotSaved;
				collider.Center = sphere.Sphere.Center;
				collider.Radius = sphere.Sphere.Radius;
				collider.Surface = sphere.Surface;

				_colliders.Add( collider );
			}

			foreach ( var capsule in part.Capsules )
			{
				var collider = go.AddComponent<CapsuleCollider>();
				collider.Flags |= ComponentFlags.NotSaved;
				collider.Start = capsule.Capsule.CenterA;
				collider.End = capsule.Capsule.CenterB;
				collider.Radius = capsule.Capsule.Radius;
				collider.Surface = capsule.Surface;

				_colliders.Add( collider );
			}

			foreach ( var hull in part.Hulls )
			{
				var collider = go.AddComponent<HullCollider>();
				collider.Flags |= ComponentFlags.NotSaved;
				collider.Type = HullCollider.PrimitiveType.Points;
				collider.Points = hull.GetPoints().ToList();
				collider.Surface = hull.Surface;

				_colliders.Add( collider );
			}
		}

		foreach ( var jointDesc in physics.Joints )
		{
			var body1 = _bodies[jointDesc.Body1].Component;
			var body2 = _bodies[jointDesc.Body2].Component;

			var localFrame1 = jointDesc.Frame1;
			var localFrame2 = jointDesc.Frame2;

			Joint joint = null;

			if ( jointDesc.Type == PhysicsGroupDescription.JointType.Hinge )
			{
				var hingeJoint = body1.AddComponent<HingeJoint>( false );

				if ( jointDesc.EnableTwistLimit )
				{
					hingeJoint.MinAngle = jointDesc.TwistMin;
					hingeJoint.MaxAngle = jointDesc.TwistMax;
				}

				if ( jointDesc.EnableAngularMotor )
				{
					var worldFrame1 = body1.WorldTransform.ToWorld( localFrame1 );
					var hingeAxis = worldFrame1.Rotation.Up;
					var targetVelocity = hingeAxis.Dot( jointDesc.AngularTargetVelocity );

					hingeJoint.Motor = HingeJoint.MotorMode.TargetVelocity;
					hingeJoint.TargetVelocity = targetVelocity.RadianToDegree();
					hingeJoint.MaxTorque = jointDesc.MaxTorque;
				}

				joint = hingeJoint;
			}
			else if ( jointDesc.Type == PhysicsGroupDescription.JointType.Ball )
			{
				var ballJoint = body1.AddComponent<BallJoint>( false );

				if ( jointDesc.EnableSwingLimit )
				{
					ballJoint.SwingLimitEnabled = true;
					ballJoint.SwingLimit = new Vector2( jointDesc.SwingMin, jointDesc.SwingMax );
				}

				if ( jointDesc.EnableTwistLimit )
				{
					ballJoint.TwistLimitEnabled = true;
					ballJoint.TwistLimit = new Vector2( jointDesc.TwistMin, jointDesc.TwistMax );
				}

				joint = ballJoint;
			}
			else if ( jointDesc.Type == PhysicsGroupDescription.JointType.Fixed )
			{
				var fixedJoint = body1.AddComponent<FixedJoint>( false );
				fixedJoint.LinearFrequency = jointDesc.LinearFrequency;
				fixedJoint.LinearDamping = jointDesc.LinearDampingRatio;
				fixedJoint.AngularFrequency = jointDesc.AngularFrequency;
				fixedJoint.AngularDamping = jointDesc.AngularDampingRatio;

				joint = fixedJoint;
			}
			else if ( jointDesc.Type == PhysicsGroupDescription.JointType.Slider )
			{
				var sliderJoint = body1.AddComponent<SliderJoint>( false );

				if ( jointDesc.EnableLinearLimit )
				{
					sliderJoint.MinLength = jointDesc.LinearMin;
					sliderJoint.MaxLength = jointDesc.LinearMax;
				}

				joint = sliderJoint;
			}

			if ( joint.IsValid() )
			{
				joint.Flags |= ComponentFlags.NotSaved;
				joint.Body = body2.GameObject;
				joint.Attachment = Joint.AttachmentMode.LocalFrames;
				joint.LocalFrame1 = localFrame1.WithPosition( jointDesc.Frame1.Position * body1.WorldScale );
				joint.LocalFrame2 = localFrame2.WithPosition( jointDesc.Frame2.Position * body2.WorldScale );
				joint.EnableCollision = jointDesc.EnableCollision;
				joint.BreakForce = jointDesc.LinearStrength;
				joint.BreakTorque = jointDesc.AngularStrength;

				_joints.Add( new JointRecord( joint, localFrame1, localFrame2 ) );
			}
		}

		_rootBody = _bodies[0].Component;

		foreach ( var body in _bodies )
		{
			body.Component.Enabled = true;
		}

		foreach ( var joint in _joints )
		{
			joint.Component.Enabled = true;
		}
	}

	private void DestroyPhysics()
	{
		foreach ( var joint in _joints )
		{
			if ( !joint.Component.IsValid() )
				continue;

			joint.Component.Destroy();
		}

		foreach ( var body in _bodies )
		{
			if ( !body.Component.IsValid() )
				continue;

			body.Component.Destroy();
		}

		foreach ( var collider in _colliders )
		{
			if ( !collider.IsValid() )
				continue;

			collider.Destroy();
		}

		_rootBody = null;

		_bodies.Clear();
		_joints.Clear();
		_colliders.Clear();

		if ( Renderer.IsValid() )
		{
			Renderer.ClearPhysicsBones();
		}
	}

	private void PositionRendererBonesFromPhysics()
	{
		if ( Scene.IsEditor )
			return;

		if ( !Renderer.IsValid() )
			return;

		Renderer.ClearPhysicsBones();

		if ( !IgnoreRoot && _rootBody.IsValid() && _rootBody.MotionEnabled )
		{
			WorldTransform = _rootBody.WorldTransform;
		}

		var world = WorldTransform;

		foreach ( var body in _bodies )
		{
			if ( !body.Component.IsValid() )
				continue;

			var local = world.ToLocal( body.Component.WorldTransform );
			Renderer.SetBoneTransform( body.Bone, local );

			if ( body.Component.MotionEnabled )
				continue;

			if ( Renderer.TryGetBoneTransformAnimation( body.Bone, out var boneWorld ) )
			{
				body.Component.SmoothMove( boneWorld, 0.01f, Time.Delta );
			}
		}
	}

	private void UpdateJointScale()
	{
		foreach ( var joint in _joints )
		{
			if ( !joint.Component.IsValid() )
				continue;

			var point1 = joint.Component.Point1;
			point1.LocalPosition = joint.Frame1.Position * joint.Component.WorldTransform.UniformScale;
			joint.Component.Point1 = point1;

			var point2 = joint.Component.Point2;
			point2.LocalPosition = joint.Frame2.Position * joint.Component.Body.WorldTransform.UniformScale;
			joint.Component.Point2 = point2;
		}
	}

	protected override void OnUpdate()
	{
		PositionRendererBonesFromPhysics();
		UpdateJointScale();
	}

	protected override void OnEnabled()
	{
		base.OnEnabled();

		CreatePhysics();
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();

		DestroyPhysics();
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();

		DestroyPhysics();
	}
}
