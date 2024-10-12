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

	private record BoneBodyPair( BoneCollection.Bone Bone, Rigidbody Body );
	private readonly List<BoneBodyPair> _bodies = new();

	[Property]
	public Model Model
	{
		get => _model;
		set
		{
			if ( _model == value )
				return;

			_model = value;

			OnModelChanged();
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
	public RigidbodyFlags RigidbodyFlags
	{
		get => _rigidBodyFlags;
		set
		{
			if ( _rigidBodyFlags == value )
				return;

			_rigidBodyFlags = value;

			foreach ( var pair in _bodies )
			{
				if ( !pair.Body.IsValid() )
					continue;

				pair.Body.RigidbodyFlags = value;
			}
		}
	}

	[Property]
	public PhysicsLock Locking
	{
		get => _locking;
		set
		{
			_locking = value;

			foreach ( var pair in _bodies )
			{
				if ( !pair.Body.IsValid() )
					continue;

				pair.Body.Locking = value;
			}
		}
	}

	/// <summary>
	/// Enable to drive renderer from physics, disable to drive physics from renderer.
	/// </summary>
	[Property]
	public bool MotionEnabled
	{
		get => _motionEnabled;
		set
		{
			if ( _motionEnabled == value )
				return;

			_motionEnabled = value;

			var boneVelocities = value ? Renderer.GetBoneVelocities() : null;

			foreach ( var pair in _bodies )
			{
				if ( !pair.Body.IsValid() )
					continue;

				pair.Body.MotionEnabled = value;

				if ( boneVelocities is not null )
				{
					var boneVelocity = boneVelocities[pair.Bone.Index];
					pair.Body.Velocity = boneVelocity.Linear;
					pair.Body.AngularVelocity = boneVelocity.Angular;
				}
			}
		}
	}

	private void OnModelChanged()
	{
		if ( !Active )
			return;

		CreatePhysics();
	}

	private void CreatePhysics()
	{
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

			if ( Renderer.IsValid() && Renderer.TryGetBoneTransform( bone, out var local ) )
			{
				// Use transform of renderer bone
				local = world.ToLocal( local );
			}
			else
			{
				// There's no renderer bones, use physics bind pose
				local = part.Transform;
			}

			var go = Scene.CreateObject( false );
			go.Flags = GameObjectFlags.NotSaved;
			go.Name = part.BoneName;
			go.LocalTransform = local;
			go.Parent = GameObject;

			var body = go.AddComponent<Rigidbody>();
			body.RigidbodyFlags = RigidbodyFlags;
			body.Locking = Locking;
			body.MotionEnabled = MotionEnabled;

			_bodies.Add( new BoneBodyPair( bone, body ) );

			foreach ( var sphere in part.Spheres )
			{
				var collider = go.AddComponent<SphereCollider>();
				collider.Center = sphere.Sphere.Center;
				collider.Radius = sphere.Sphere.Radius;
				collider.Surface = sphere.Surface;
			}

			foreach ( var capsule in part.Capsules )
			{
				var collider = go.AddComponent<CapsuleCollider>();
				collider.Start = capsule.Capsule.CenterA;
				collider.End = capsule.Capsule.CenterB;
				collider.Radius = capsule.Capsule.Radius;
				collider.Surface = capsule.Surface;
			}

			foreach ( var hull in part.Hulls )
			{
				var collider = go.AddComponent<HullCollider>();
				collider.Type = HullCollider.PrimitiveType.Points;
				collider.Points = hull.GetPoints().ToList();
				collider.Surface = hull.Surface;
			}
		}

		foreach ( var jointDesc in physics.Joints )
		{
			Joint joint = null;
			var body = _bodies[jointDesc.Body1].Body;
			var body2 = _bodies[jointDesc.Body2].Body;

			var localFrame1 = jointDesc.Frame1.WithPosition( jointDesc.Frame1.Position * body.WorldScale );
			var localFrame2 = jointDesc.Frame2.WithPosition( jointDesc.Frame2.Position * body2.WorldScale );

			if ( jointDesc.Type == PhysicsGroupDescription.JointType.Hinge )
			{
				var hingeJoint = body.AddComponent<HingeJoint>();
				hingeJoint.Attachment = HingeJoint.AttachmentMode.LocalFrames;
				hingeJoint.LocalFrame1 = localFrame1;
				hingeJoint.LocalFrame2 = localFrame2;

				if ( jointDesc.EnableTwistLimit )
				{
					hingeJoint.MinAngle = jointDesc.TwistMin;
					hingeJoint.MaxAngle = jointDesc.TwistMax;
				}

				joint = hingeJoint;
			}
			else if ( jointDesc.Type == PhysicsGroupDescription.JointType.Ball )
			{
				var ballJoint = body.AddComponent<BallJoint>();
				ballJoint.Attachment = BallJoint.AttachmentMode.LocalFrames;
				ballJoint.LocalFrame1 = localFrame1;
				ballJoint.LocalFrame2 = localFrame2;

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
				var fixedJoint = body.AddComponent<FixedJoint>();

				joint = fixedJoint;
			}
			else if ( jointDesc.Type == PhysicsGroupDescription.JointType.Slider )
			{
				var sliderJoint = body.AddComponent<SliderJoint>();

				joint = sliderJoint;
			}

			if ( joint.IsValid() )
			{
				joint.Body = body2.GameObject;
				joint.EnableCollision = jointDesc.EnableCollision;
				joint.BreakForce = jointDesc.LinearStrength;
				joint.BreakTorque = jointDesc.AngularStrength;
			}
		}

		foreach ( var pair in _bodies )
		{
			pair.Body.GameObject.Enabled = true;
		}
	}

	private void DestroyPhysics()
	{
		foreach ( var pair in _bodies )
		{
			if ( !pair.Body.IsValid() )
				continue;

			pair.Body.DestroyGameObject();
		}

		_bodies.Clear();

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

		var world = WorldTransform;

		foreach ( var pair in _bodies )
		{
			if ( !pair.Body.IsValid() )
				continue;

			if ( pair.Body.MotionEnabled )
			{
				var local = world.ToLocal( pair.Body.WorldTransform );
				Renderer.SetBoneTransform( pair.Bone, local );
			}
			else
			{
				if ( Renderer.TryGetBoneTransform( pair.Bone, out var local ) )
				{
					pair.Body.LocalTransform = world.ToLocal( local );
				}
			}
		}
	}

	protected override void OnUpdate()
	{
		PositionRendererBonesFromPhysics();
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
