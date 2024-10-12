namespace Sandbox;

/// <summary>
/// Work in progress ragdoll component that creates child rigid bodies from model physics.
/// </summary>
public sealed class Ragdoll : Component, Component.ExecuteInEditor
{
	Model _model;

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
	public SkinnedModelRenderer Renderer { get; set; }

	private record BoneBodyPair( BoneCollection.Bone Bone, GameObject Body );
	private readonly List<BoneBodyPair> _bodies = new();

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

		foreach ( var part in physics.Parts )
		{
			var go = Scene.CreateObject( false );
			go.Flags = GameObjectFlags.NotSaved;
			go.Name = part.BoneName;
			go.LocalTransform = part.Transform;
			go.Parent = GameObject;
			go.AddComponent<Rigidbody>();

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

			var bone = Model.Bones.GetBone( part.BoneName );
			_bodies.Add( new BoneBodyPair( bone, go ) );
		}

		foreach ( var jointDesc in physics.Joints )
		{
			Joint joint = null;
			var body = _bodies[jointDesc.Body1].Body;

			if ( jointDesc.Type == PhysicsGroupDescription.JointType.Hinge )
			{
				var hingeJoint = body.AddComponent<HingeJoint>();
				hingeJoint.Attachment = HingeJoint.AttachmentMode.LocalFrames;
				hingeJoint.LocalFrame1 = jointDesc.Frame1;
				hingeJoint.LocalFrame2 = jointDesc.Frame2;

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
				ballJoint.LocalFrame1 = jointDesc.Frame1;
				ballJoint.LocalFrame2 = jointDesc.Frame2;

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
				joint.Body = _bodies[jointDesc.Body2].Body;
				joint.EnableCollision = jointDesc.EnableCollision;
				joint.BreakForce = jointDesc.LinearStrength;
				joint.BreakTorque = jointDesc.AngularStrength;
			}
		}

		foreach ( var pair in _bodies )
		{
			pair.Body.Enabled = true;
		}
	}

	private void DestroyPhysics()
	{
		foreach ( var pair in _bodies )
		{
			if ( !pair.Body.IsValid() )
				continue;

			pair.Body.Destroy();
		}

		_bodies.Clear();

		if ( Renderer.IsValid() )
		{
			Renderer.ClearPhysicsBones();
		}
	}

	private void PositionRendererBonesFromPhysics()
	{
		if ( !Renderer.IsValid() )
			return;

		var world = WorldTransform;

		foreach ( var pair in _bodies )
		{
			if ( !pair.Body.IsValid() )
				continue;

			var local = world.ToLocal( pair.Body.WorldTransform );
			Renderer.SetBoneTransform( pair.Bone, local );
		}
	}

	protected override void OnUpdate()
	{
		if ( !Renderer.IsValid() )
			return;

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
}
