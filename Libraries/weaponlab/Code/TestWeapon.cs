using Sandbox;

public sealed class TestWeapon : Component
{
	[Property, Group( "View Model" )]
	public Model ViewModel { get; set; }

	[Property, Group( "View Model" )]
	public Vector3 ViewModelOffset { get; set; }

	[Property, Group( "Primary" )]
	public bool PrimaryAutomatic { get; set; } = true;

	[Property, Group( "Primary" )]
	public float PrimaryDelay { get; set; } = 0.05f;

	[Property, Group( "Primary" )]
	public SoundEvent PrimaryAttackSound { get; set; }

	[Property]
	public GameObject MuzzlePrefab { get; set; }

	[Property]
	public GameObject WeaponPrefab { get; set; }

	[Property]
	public AnimationGraph GraphOverride { get; set; }

	/// <summary>
	/// Will copy some parameters from the body renderer
	/// </summary>
	[Property]
	public SkinnedModelRenderer BodyRenderer { get; set; }

	float ironsights;

	protected override void OnUpdate()
	{
		PositionViewmodel();

		AnimationThink();
	}

	Rotation lastRot;

	void AnimationThink()
	{
		if ( !(viewmodel?.Components.TryGet<SkinnedModelRenderer>( out var vm ) ?? false) )
			return;

		var cc = Components.Get<CharacterController>( FindMode.InAncestors );

		ironsights = ironsights.LerpTo( Input.Down( "attack2" ) ? 1 : 0, Time.Delta * 5.0f );
		vm.Set( "iconsights", ironsights );
		vm.Set( "move_bob", 1 );

		var rotationDelta = Rotation.Difference( lastRot, Scene.Camera.Transform.Rotation );
		lastRot = Scene.Camera.Transform.Rotation;

		var angles = rotationDelta.Angles();

		vm.Set( "aim_pitch", angles.pitch );
		vm.Set( "aim_yaw", angles.yaw );

		vm.Set( "aim_pitch_inertia", angles.pitch );
		vm.Set( "aim_yaw_inertia", angles.yaw );

		if ( BodyRenderer.IsValid() )
		{
			vm.Set( "jump", BodyRenderer.GetBool( "jump" ) );
			vm.Set( "move_groundspeed", BodyRenderer.GetFloat( "move_groundspeed" ) );
			vm.Set( "b_grounded", BodyRenderer.GetFloat( "b_grounded" ) );
			vm.Set( "move_x", BodyRenderer.GetFloat( "move_x" ) );
			vm.Set( "move_y", BodyRenderer.GetFloat( "move_y" ) );
			vm.Set( "move_z", BodyRenderer.GetFloat( "move_z" ) );
		}
	}

	GameObject viewmodel;

	protected override void OnEnabled()
	{
		base.OnEnabled();

		CreateViewmodel();
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();

		viewmodel?.Destroy();
	}

	protected override void OnFixedUpdate()
	{
		if ( Input.Down( "attack1" ) )
		{
			RunAttack();
		}

		if ( Input.Down( "reload" ) )
		{
			if ( viewmodel.Components.TryGet<SkinnedModelRenderer>( out var vm ) )
			{
				vm.Set( "b_reload", true );
			}
		}

		if ( Input.Down( "attack2" ) )
		{
			RunAltAttack();
		}
	}

	TimeSince timeSinceLastShoot = 0.0f;

	void RunAttack()
	{
		if ( !PrimaryAutomatic && !Input.Pressed( "attack1" ) )
			return;

		if ( timeSinceLastShoot < PrimaryDelay )
			return;

		timeSinceLastShoot = 0;

		Vector3 shootPosition = Transform.Position;

		if ( viewmodel.Components.TryGet<SkinnedModelRenderer>( out var vm ) )
		{
			vm.Set( "b_attack", true );

			var muzzle = vm.GetBoneObject( vm.Model.Bones.GetBone( "muzzle" ) ) ?? vm.GameObject;

			shootPosition = muzzle.Transform.Position;
			GameObject.Clone( "/effects/muzzle.prefab", global::Transform.Zero, muzzle );
		}

		Sound.Play( PrimaryAttackSound, shootPosition );

		ShootBullet();

	}

	void RunAltAttack()
	{
	}

	void ShootBullet()
	{
		var ray = Scene.Camera.Transform.World.ForwardRay;
		ray.Forward += Vector3.Random * 0.01f;

		var tr = Scene.Trace.Ray( ray, 4096 )
					.IgnoreGameObjectHierarchy( GameObject.Parent )
					.Run();

		//Sound.Play( shootSound, Transform.Position );

		if ( !tr.Hit || tr.GameObject is null )
			return;

		GameObject.Clone( "/effects/impact_default.prefab", new Transform( tr.HitPosition + tr.Normal * 2.0f, Rotation.LookAt( tr.Normal ) ) );

		{
			var go = GameObject.Clone( "/effects/decal_bullet_default.prefab" );
			go.Transform.World = new Transform( tr.HitPosition + tr.Normal * 2.0f, Rotation.LookAt( -tr.Normal, Vector3.Random ), System.Random.Shared.Float( 0.8f, 1.2f ) );
			go.SetParent( tr.GameObject );
		}

		if ( tr.Body.IsValid() )
		{
			tr.Body.ApplyImpulseAt( tr.HitPosition, tr.Direction * 200.0f * tr.Body.Mass.Clamp( 0, 200 ) );
		}

		var damage = new DamageInfo( 10, GameObject, GameObject, tr.Hitbox );
		damage.Position = tr.HitPosition;
		damage.Shape = tr.Shape;

		foreach ( var damageable in tr.GameObject.Components.GetAll<IDamageable>() )
		{
			damageable.OnDamage( damage );
		}
	}

	void CreateViewmodel()
	{
		viewmodel = new GameObject( true, "viewmodel" );

		var modelRender = viewmodel.Components.Create<SkinnedModelRenderer>();
		modelRender.Model = ViewModel;
		modelRender.CreateBoneObjects = true;

		if ( GraphOverride is not null )
			modelRender.AnimationGraph = GraphOverride;

		{
			var arms = new GameObject();
			arms.Parent = viewmodel;

			var model = arms.Components.Create<SkinnedModelRenderer>();
			model.Model = Model.Load( "models/first_person/first_person_arms.vmdl" );
			model.BoneMergeTarget = modelRender;
		}
	}

	void PositionViewmodel()
	{
		if ( viewmodel is null ) return;

		var targetPos = Scene.Camera.Transform.World;

		targetPos.Position += targetPos.Rotation * ViewModelOffset;

		viewmodel.Transform.World = targetPos;
	}
}
