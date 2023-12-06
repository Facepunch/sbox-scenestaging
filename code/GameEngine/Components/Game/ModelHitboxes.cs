using Sandbox;

/// <summary>
/// Hitboxes from a model
/// </summary>
[Title( "Hitboxes From Model" )]
[Category( "Game" )]
[Icon( "psychology_alt" )]
public sealed class ModelHitboxes : BaseComponent, BaseComponent.ExecuteInEditor
{
	HitboxSystem system;


	/// <summary>
	/// The target SkinnedModelRenderer that holds the model/skeleton you want to 
	/// take the hitboxes from.
	/// </summary>
	[Property, MakeDirty] public SkinnedModelRenderer Renderer { get; set; }

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

	protected override void OnAwake()
	{
		Scene.GetSystem( out system );
	}

	protected override void OnEnabled()
	{
		Rebuild();
	}

	protected override void OnDirty()
	{
		Rebuild();
	}

	protected override void OnDisabled()
	{
		Clear();
	}

	public void Rebuild()
	{
		if ( system is null )
			return;

		Clear();
		AddFrom( Renderer );
	}

	void Clear()
	{
		foreach( var h in Hitboxes )
		{
			h.Dispose();
		}

		Hitboxes.Clear();
	}

	private void AddFrom( SkinnedModelRenderer anim )
	{
		if ( anim is null )
			return;

		if ( anim.Model is null )
			return;

		foreach ( var hitbox in anim.Model.HitboxSet.All )
		{
			if ( hitbox.Bone is null )
				continue;

			var tx = anim.GetBoneTransform( hitbox.Bone, true );

			var body = new PhysicsBody( system.PhysicsWorld );

			if ( hitbox.Shape is Sphere sphere )
			{
				var shape = body.AddSphereShape( sphere.Center, sphere.Radius );
				shape.Tags.SetFrom( GameObject.Tags );
			}

			if ( hitbox.Shape is Capsule capsule )
			{
				var shape = body.AddCapsuleShape( capsule.CenterA, capsule.CenterB, capsule.Radius );
				shape.Tags.SetFrom( GameObject.Tags );
			}

			if ( hitbox.Shape is BBox box )
			{
				var shape = body.AddBoxShape( box.Center, Rotation.Identity, box.Size ); // might have to be Size * 0.5?
				shape.Tags.SetFrom( GameObject.Tags );
			}

			body.Transform = tx;

			var b = new Hitbox( Target ?? GameObject, hitbox.Bone, hitbox.Tags, body );
			AddHitbox( b );
		}
	}

	public void UpdatePositions()
	{
		if ( Renderer is null ) return;

		foreach ( var hitbox in Hitboxes )
		{
			hitbox.Body.Transform = Renderer.GetBoneTransform( hitbox.Bone, true );
		}
	}

	List<Hitbox> Hitboxes = new List<Hitbox>();

	public void AddHitbox( Hitbox hitbox )
	{
		Hitboxes.Add( hitbox );
	}
}
