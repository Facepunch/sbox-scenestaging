using Sandbox;
using Sandbox.Physics;

public abstract class Joint : Component, Component.ExecuteInEditor
{
	private PhysicsJoint joint;
	private PhysicsBody worldBody;

	/// <summary>
	/// Game object to attach this joint to, uses world reference body if not specified.
	/// </summary>
	[Property] public GameObject Body { get; set; }

	private bool enableCollision;

	private bool started;

	/// <summary>
	/// Enable or disable collision between the two bodies.
	/// </summary>
	[Property] public bool EnableCollision
	{
		get => enableCollision;
		set
		{
			enableCollision = value;
			if ( joint.IsValid() )
				joint.Collisions = enableCollision;
		}
	}

	/// <summary>
	/// Strength of the linear constraint. If it takes any more energy than this, it'll break.
	/// </summary>
	[Property]
	public float BreakForce
	{
		get => joint.IsValid() ? joint.Strength : default;
		set
		{
			if ( joint.IsValid() )
				joint.Strength = value;
		}
	}

	/// <summary>
	/// Strength of the angular constraint. If it takes any more energy than this, it'll break.
	/// </summary>
	[Property]
	public float BreakTorque
	{
		get => joint.IsValid() ? joint.AngularStrength : default;
		set
		{
			if ( joint.IsValid() )
				joint.AngularStrength = value;
		}
	}

	protected override void OnStart()
	{
		base.OnStart();

		started = true;

		CreateJoint();
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();

		started = false;

		DestroyJoint();
	}

	protected override void OnEnabled()
	{
		base.OnEnabled();

		CreateJoint();
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();

		DestroyJoint();
	}

	/// <summary>
	/// Joint type implementation.
	/// </summary>
	protected abstract PhysicsJoint CreateJoint( PhysicsBody body1, PhysicsBody body2 );

	private void CreateJoint()
	{
		if ( !started )
			return;

		var thisPhysics = Components.Get<PhysicsComponent>();
		if ( thisPhysics == null || !thisPhysics.GetBody().IsValid() )
			return;

		// By default use the world reference body.
		var otherBody = Scene?.PhysicsWorld?.Body;
		if ( Body.IsValid() )
		{
			// Try to grab body from target game object.
			var otherPhysics = Body.Components.Get<PhysicsComponent>();
			if ( otherPhysics != null && otherPhysics.GetBody().IsValid() )
				otherBody = otherPhysics.GetBody();

			if ( !otherBody.IsValid() )
			{
				var otherCollider = Body.Components.Get<Collider>();
				if ( otherCollider != null && otherCollider.KeyframeBody.IsValid() )
					otherBody = otherCollider.KeyframeBody;
			}
		}

		var thisBody = thisPhysics.GetBody();
		if ( !otherBody.IsValid() )
		{
			// Create a new world reference body if all else fails.
			// This shouldn't be needed when scenes sets the world reference body.
			worldBody = new PhysicsBody( thisBody.World );
			otherBody = worldBody;
		}

		joint = CreateJoint( thisBody, otherBody );
		joint.Collisions = EnableCollision;
	}

	private void DestroyJoint()
	{
		joint?.Remove();
		joint = null;

		worldBody?.Remove();
		worldBody = null;
	}

	protected override void DrawGizmos()
	{
		if ( !joint.IsValid() )
			return;

		Gizmo.Draw.LineThickness = 1;
		Gizmo.Draw.Color = Gizmo.Colors.Green.WithAlpha( Gizmo.IsSelected ? 1.0f : 0.2f );
		Gizmo.Draw.Line( joint.Point1.LocalPosition, 0 );

		if ( Body.IsValid() )
		{
			Gizmo.Draw.Line( joint.Point1.LocalPosition, Transform.World.PointToLocal( Body.Transform.Position ) );
		}
	}
}
