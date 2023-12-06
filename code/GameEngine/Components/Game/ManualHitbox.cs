using Sandbox;

/// <summary>
/// Hitboxes from a model
/// </summary>
[Title( "Hitbox" )]
[Category( "Game" )]
[Icon( "psychology_alt" )]
public sealed class ManualHitbox : Component, Component.ExecuteInEditor
{
	HitboxSystem system;

	Hitbox hitbox;
	GameObject _target;

	/// <summary>
	/// The target GameObject to report in trace hits. If this is unset we'll defaault to the gameobject on which this component is.
	/// </summary>
	[Property]
	public GameObject Target
	{
		get => _target;
		set
		{
			if ( _target == value ) return;

			_target = value;
			Rebuild();
		}
	}

	public enum HitboxShape
	{
		Sphere,
		Capsule,
		Box
	}

	[Property, MakeDirty] public HitboxShape Shape { get; set; } = HitboxShape.Sphere;

	[Property, MakeDirty] public float Radius { get; set; } = 10.0f;

	[Property, MakeDirty] public Vector3 CenterA { get; set; }

	[Property, MakeDirty] public Vector3 CenterB { get; set; }
	[Property, MakeDirty] public GameTags HitboxTags { get; set; }


	protected override void OnAwake()
	{
		Scene.GetSystem( out system );
	}

	protected override void DrawGizmos()
	{
		if ( !Gizmo.IsSelected )
			return;

		if ( Shape == HitboxShape.Sphere )
		{
			Gizmo.Draw.LineSphere( CenterA, Radius );
		}

		if ( Shape == HitboxShape.Capsule )
		{
			Gizmo.Draw.LineCapsule( new Capsule( CenterA, CenterB, Radius ) );
		}
	}

	protected override void OnDirty()
	{
		Rebuild();
	}

	protected override void OnEnabled()
	{
		Rebuild();
	}

	protected override void OnDisabled()
	{
		hitbox?.Dispose();
		hitbox = null;
	}

	public void Rebuild()
	{
		hitbox?.Dispose();
		hitbox = null;

		var body = new PhysicsBody( system.PhysicsWorld );
		var tx = Transform.World;

		if ( Shape == HitboxShape.Sphere )
		{
			var shape = body.AddSphereShape( CenterA * tx.Scale, Radius * tx.Scale );
			shape.Tags.SetFrom( GameObject.Tags );
		}

		if ( Shape == HitboxShape.Capsule )
		{
			var shape = body.AddCapsuleShape( CenterA * tx.Scale, CenterB * tx.Scale, Radius * tx.Scale );
			shape.Tags.SetFrom( GameObject.Tags );
		}

		if ( Shape == HitboxShape.Box )
		{
			var shape = body.AddBoxShape( CenterA, Rotation.Identity, CenterB ); // might have to be Size * 0.5?
			shape.Tags.SetFrom( GameObject.Tags );
		}

		body.Transform = tx.WithScale( 1 );

		hitbox = new Hitbox( Target ?? GameObject, HitboxTags, body );
	}

	public void UpdatePositions()
	{
		if ( hitbox is null ) return;

		hitbox.Body.Transform = Transform.World;
	}

}


