using Sandbox.Diagnostics;
using Sandbox.ModelEditor.Nodes;

public class Prop : Component, Component.ExecuteInEditor, Component.IDamageable
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

	[Property, Sync] public float Health { get; set; }
	[Property, MakeDirty] public bool ShowCreatedComponents { get; set; }

	List<Component> ProceduralComponents { get; set; }

	[Property] public Action OnPropBreak { get; set; }
	[Property] public Action<DamageInfo> OnPropTakeDamage { get; set; }

	public void ClearProcedurals()
	{
		if ( ProceduralComponents is null )
			return;

		foreach ( var p in ProceduralComponents )
		{
			p.Destroy();
		}

		ProceduralComponents?.Clear();
	}

	public void AddProcedural( Component p )
	{
		Assert.AreNotEqual( p, this );

		ProceduralComponents ??= new();

		p.Flags |= ComponentFlags.Hidden | ComponentFlags.NotSaved;

		if ( !ProceduralComponents.Contains( p ) )
		{
			ProceduralComponents.Add( p );
		}
	}


	protected override void OnEnabled()
	{
		UpdateComponents();
	}

	protected override void OnDisabled()
	{
		ClearProcedurals();
	}

	void OnModelChanged()
	{
		if ( Model is null )
			return;

		if ( Model.TryGetData<ModelPropData>( out var propData ) )
		{
			Health = propData.Health > 0 ? propData.Health : Health;
		}

		if ( Active )
		{
			ClearProcedurals();
			UpdateComponents();
		}
	}

	void UpdateComponents()
	{
		if ( Model is null )
			return;

		bool skinned = Model.BoneCount > 0;

		CreateModelComponent( skinned );
		CreatePhysicsComponent();
		ApplyVisibilityFlags();
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

		AddProcedural( mr );
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

			AddProcedural( collider );

			var rigidBody = Components.GetOrCreate<Rigidbody>();
			rigidBody.Flags |= ComponentFlags.Hidden | ComponentFlags.NotSaved;

			AddProcedural( rigidBody );

			return;
		}

		var p = Components.GetOrCreate<ModelPhysics>();
		p.Renderer = ProceduralComponents.OfType<SkinnedModelRenderer>().FirstOrDefault();
		p.Model = Model;
		AddProcedural( p );
	}


	public void OnDamage( in DamageInfo damage )
	{
		// The dead feel nothing
		if ( Health <= 0.0f )
			return;

		OnPropTakeDamage?.Invoke( damage );

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
		OnBreak();
		GameObject.Destroy();
	}

	void OnBreak()
	{
		OnPropBreak?.Invoke();

		CreateGibs();
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

	protected override void OnDirty()
	{
		base.OnDirty();

		ApplyVisibilityFlags();
	}

	void ApplyVisibilityFlags()
	{
		if ( ProceduralComponents is null )
			return;

		foreach ( var c in ProceduralComponents )
		{
			if ( ShowCreatedComponents )
			{
				c.Flags = ComponentFlags.NotSaved;
			}
			else
			{
				c.Flags = ComponentFlags.Hidden | ComponentFlags.NotSaved;
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
