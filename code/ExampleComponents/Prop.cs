using Sandbox.ModelEditor.Nodes;

public class Prop : Component, Component.ExecuteInEditor, Component.ICollisionListener, Component.IDamageable
{
	Model _model;

	[Property]
	public Model Model
	{
		get => _model;
		set
		{
			if ( _model == value ) return;
			_model = value;
			OnModelChanged();
		}
	}
	[Property] public float Health { get; set; }

	protected override void OnEnabled()
	{
		OnModelChanged();
		UpdateComponents();
	}

	void OnModelChanged()
	{
		if ( Model is null )
			return;

		if ( Model.TryGetData<ModelPropData>( out var propData ) )
		{
			Health = propData.Health > 0 ? propData.Health : Health;
		}
	}

	void UpdateComponents()
	{
		if ( Model is null )
			return;

		bool skinned = Model.BoneCount > 0;

		CreateModelComponent( skinned );
		CreatePhysicsComponent();
	}

	void CreateModelComponent( bool skinned )
	{
		ModelRenderer mr;

		if ( skinned )
		{
			mr = Components.GetOrCreate<SkinnedModelRenderer>();
		}
		else
		{
			mr = Components.GetOrCreate<ModelRenderer>();
		}

		mr.Flags |= ComponentFlags.Hidden | ComponentFlags.NotSaved;
		mr.Model = Model;
	}

	void CreatePhysicsComponent()
	{
		if ( Model.Physics.Parts.Count == 0 )
			return;

		if ( Model.Physics.Parts.Count == 1 )
		{
			var collider = Components.GetOrCreate<ModelCollider>();
			collider.Flags |= ComponentFlags.Hidden | ComponentFlags.NotSaved;
			collider.Model = Model;

			var rigidBody = Components.GetOrCreate<Rigidbody>();
			rigidBody.Flags |= ComponentFlags.Hidden | ComponentFlags.NotSaved;

			return;
		}

		var p = Components.GetOrCreate<ModelPhysics>();
		p.Model = Model;
	}

	public void OnCollisionStart( Collision c )
	{
		//Log.Warning( $"{c.Contact.NormalSpeed} / {c.Contact.Speed.Length}" );
	}

	public void OnCollisionUpdate( Collision other )
	{

	}

	public void OnCollisionStop( CollisionStop other )
	{

	}

	public void OnDamage( in DamageInfo damage )
	{
		// The dead feel nothing
		if ( Health <= 0.0f )
			return;

		// Take the damage
		Health -= damage.Damage;

		if ( Health <= 0 )
		{
			Kill();
			Health = 0;
		}
	}

	public void Kill()
	{
		CreateGibs();
		GameObject.Destroy();
	}

	void CreateGibs()
	{
		if ( Model is null )
			return;

		var rb = Components.Get<Rigidbody>();

		var breaklist = Model.GetData<ModelBreakPiece[]>();

		if ( breaklist == null || breaklist.Length <= 0 ) return;

		foreach ( var model in breaklist )
		{
			var gib = new GameObject( true, $"{GameObject.Name} (gib)" );

			gib.Transform.Position = Transform.World.PointToWorld( model.Offset );
			gib.Transform.Rotation = Transform.Rotation;

			foreach ( var tag in model.CollisionTags.Split( ' ', StringSplitOptions.RemoveEmptyEntries ) )
			{
				gib.Tags.Add( tag );
			}

			var c = gib.Components.Create<Gib>( false );
			c.FadeTime = model.FadeTime;
			c.Model = Model.Load( model.Model );
			c.Enabled = true;

			var phys = gib.Components.Get<Rigidbody>( true );

			if ( phys is not null )
			{
				phys.Velocity = rb.Velocity;
				phys.AngularVelocity = rb.AngularVelocity;
			}


		}
	}
}

public class Gib : Prop
{
	public float FadeTime { get; set; }

	protected override void OnEnabled()
	{
		base.OnEnabled();

		if ( FadeTime > 0 && !Scene.IsEditor )
		{
			_ = RunGib();
		}
	}

	async Task RunGib()
	{
		await Task.DelaySeconds( FadeTime + Random.Shared.Float( 0, 2.0f ) );

		if ( !IsValid )
			return;

		var modelComponent = Components.Get<ModelRenderer>();
		if ( modelComponent is not null )
		{
			for ( float f = modelComponent.Tint.a; f > 0.0f; f -= Time.Delta )
			{
				modelComponent.Tint = modelComponent.Tint.WithAlpha( f );
				await Task.Frame();
			}
		}

		GameObject.Destroy();
	}
}
