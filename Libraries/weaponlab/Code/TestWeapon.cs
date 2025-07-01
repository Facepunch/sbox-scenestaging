using Sandbox;
using Sandbox.Rendering;
using System;


public enum GrabAction
{
	None,
	SweepDown,
	SweepRight,
	SweepLeft,
	PushButton
}

public interface IGrabAction
{
	/// <summary>
	/// Returns the grab action for this component
	/// </summary>
	GrabAction GrabAction { get; }
}

public sealed class TestWeapon : Component, PlayerController.IEvents, ICameraSetup
{
	[Property, Group( "View Model" )]
	public Model ViewModel { get; set; }

	[Property, Group( "View Model" ), Model.BodyGroupMask( ModelParameter = "ViewModel" )]
	public ulong Bodygroups { get; set; }

	[Property, Group( "View Model" )]
	public Vector3 ViewModelLocalOffset { get; set; }

	[Property, Group( "View Model" )]
	public Angles ViewModelLocalRotation { get; set; }

	[Property, Group( "Primary" )]
	public bool PrimaryAutomatic { get; set; } = true;

	[Property, Group( "Primary" )]
	public float PrimaryDelay { get; set; } = 0.05f;

	[Property, Group( "Primary" )]
	public SoundEvent PrimaryAttackSound { get; set; }

	[Property, Group( "Config" )]
	public AnimationGraph GraphOverride { get; set; }

	[Property, Group( "Config" )]
	public float RayDistance { get; set; } = 4096;

	[Property, Group( "Ammo" )]
	public int Ammo { get; set; } = 30;

	[Property, Group( "Ammo" )]
	public int MaxAmmo { get; set; } = 30;

	[Property, Group( "Ammo" )]
	public float ReloadTime { get; set; } = 1.5f;

	[Property, Group( "View Model" )]
	public GrabAction UseGrabAction { get; set; }

	[Property, Group( "Weapon-Specific" )]
	public bool IsBoltAction { get; set; } = false;

	[Property, Group( "Weapon-Specific" )]
	public bool UseMuzzleFlash { get; set; } = true;

	[Property, Group( "Weapon-Specific" )]
	public bool SingularReload { get; set; } = false;

	[Property, Group( "Weapon-Specific" )]
	public bool TwoHanded { get; set; } = false;

	[Property, Group( "Weapon-Specific" )]
	public bool UseJoystick { get; set; } = false;

	[Property, Group( "Weapon-Specific" ), Range( 0, 2, 0.05f )]
	public float IronsightsFireScale { get; set; } = 0.5f;

	[Property, Group( "Recoil" )]
	public RangedFloat PitchRecoil { get; set; } = new RangedFloat( -0.25f, -0.5f );

	[Property, Group( "Recoil" )]
	public RangedFloat YawRecoil { get; set; } = new RangedFloat( -0.4f, 0.4f );

	[Property, Group( "Recoil" )]
	public float RecoilNoiseScale { get; set; } = 1.0f;

	[Property, Group( "Recoil" )]
	public float RecoilNoiseSpeed { get; set; } = 0.3f;

	/// <summary>
	/// Will copy some parameters from the body renderer
	/// </summary>
	[Property, Group( "Config" )]
	public SkinnedModelRenderer BodyRenderer { get; set; }

	private enum FireMode
	{
		Off,
		Single,
		Burst,
		FullAuto
	}

	FireMode fireMode = FireMode.Off;
	bool isReloading = false;
	TimeSince timeSinceReload = 0.0f;

	public bool IsAiming => Input.Down( "attack2" ) && !UseJoystick;

	void PlayerController.IEvents.StartPressing( Component target )
	{
		if ( !(viewmodel?.Components.TryGet<SkinnedModelRenderer>( out var vm ) ?? false) )
			return;

		if ( target is not IGrabAction grabber ) return;

		vm.Set( "grab_action", (int)grabber.GrabAction );
	}

	protected override void OnUpdate()
	{
		if ( Input.Pressed( "Drop" ) )
		{
			ToggleLower();
		}

		var aimPos = Screen.Size * 0.5f;
		DrawHud( Scene.Camera.Hud, aimPos );
		AnimationThink();
	}

	void ToggleLower()
	{
		lower = !lower;
		timeSinceLowered = 0;
	}

	bool lower = false;
	TimeSince timeSinceLowered = 0.0f;
	Rotation lastRot;
	TimeSince timeSincePrimaryAttackStarted = 0.0f;
	Vector3.SmoothDamped smoothedWish = new Vector3.SmoothDamped( 0, 0, 0.5f );
	Vector3.SmoothDamped smoothedJoystick = new Vector3.SmoothDamped( 0, 0, 0.5f );

	private static Vector3 GetLocalVelocity( Rotation rotation, Vector3 worldVelocity )
	{
		// TODO: this could be rotation.Inverse * worldVelocity

		var forward = rotation.Forward.Dot( worldVelocity );
		var sideward = rotation.Right.Dot( worldVelocity );

		return new Vector3( forward, sideward, worldVelocity.z );
	}

	private static float GetAngle( Vector3 localVelocity )
	{
		return MathF.Atan2( localVelocity.y, localVelocity.x ).RadianToDegree().NormalizeDegrees();
	}


	void AnimationThink()
	{
		if ( !(viewmodel?.Components.TryGet<SkinnedModelRenderer>( out var vm ) ?? false) )
			return;

		var controller = Components.Get<PlayerController>( FindMode.InAncestors );

		var vel = controller.Velocity;
		var wishVel = controller.WishVelocity;
		var rot = vm.WorldRotation;
		var isAiming = IsAiming;
		var wantsSprint = Input.Down( controller.AltMoveButton );

		var isHovering = controller.Hovered.IsValid() && controller.Hovered.WorldPosition.Distance( WorldPosition ) < 128;

		vm.Set( "b_grab", isHovering );

		//
		// General states
		//
		vm.Set( "ironsights", isAiming && !wantsSprint ? 1 : 0 );
		vm.Set( "b_sprint", wantsSprint && vel.Length > 0f );
		vm.Set( "b_lower_weapon", lower );
		vm.Set( "firing_mode", (int)fireMode );
		vm.Set( "b_empty", Ammo < 1 );
		vm.Set( "b_twohanded", TwoHanded );
		vm.Set( "ironsights_fire_scale", IronsightsFireScale );

		controller.UseLookControls = !(UseJoystick && Input.Down( "Attack2" ));

		//
		// Joystick
		//
		{
			vm.Set( "b_joystick", UseJoystick && Input.Down( "Attack2" ) );

			var smoothed = Mouse.Delta * 0.1f;
			{
				smoothedJoystick.Target = smoothed;
				smoothedJoystick.SmoothTime = 0.1f;
				smoothedJoystick.Update( Time.Delta );
				smoothed = smoothedJoystick.Current;
			}

			vm.Set( "joystick_x", UseJoystick ? smoothed.y : 0f );
			vm.Set( "joystick_y", UseJoystick ? -smoothed.x : 0f );
		}

		//
		// Bolt action
		//
		if ( Input.Released( "Attack1" ) && IsBoltAction )
		{
			vm.Set( "b_reload_bolt", true );
		}

		//
		// Movement
		//
		{
			var smoothed = wishVel;
			{
				smoothedWish.Target = smoothed;
				smoothedWish.SmoothTime = 0.1f;
				smoothedWish.Update( Time.Delta );
				smoothed = smoothedWish.Current;
			}

			smoothed = GetLocalVelocity( rot, smoothed );

			vm.Set( "move_direction", GetAngle( smoothed ) );
			vm.Set( "move_bob", smoothed.Length.Remap( 0, 150, 0, 1 ) );
			vm.Set( "move_groundspeed", smoothed.WithZ( 0f ).Length );
			vm.Set( "move_x", smoothed.x );
			vm.Set( "move_y", smoothed.y );
			vm.Set( "move_z", smoothed.z );
		}

		vm.Set( "attack_hold", CanShoot() && Input.Down( "Attack1" ) ? timeSincePrimaryAttackStarted : 0 );

		var rotationDelta = Rotation.Difference( lastRot, Scene.Camera.WorldRotation );
		lastRot = Scene.Camera.WorldRotation;

		var angles = rotationDelta.Angles();

		vm.Set( "aim_pitch", angles.pitch );
		vm.Set( "aim_yaw", angles.yaw );

		vm.Set( "aim_pitch_inertia", angles.pitch );
		vm.Set( "aim_yaw_inertia", angles.yaw );

		if ( BodyRenderer.IsValid() )
		{
			vm.Set( "b_jump", BodyRenderer.GetBool( "b_jump" ) );
			vm.Set( "move_groundspeed", BodyRenderer.GetFloat( "move_groundspeed" ) );
			vm.Set( "b_grounded", controller.IsOnGround );
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

	bool CanShoot()
	{
		if ( isReloading ) return false;

		if ( !PrimaryAutomatic && !Input.Pressed( "attack1" ) )
			return false;

		if ( timeSinceLastShoot < PrimaryDelay )
			return false;

		if ( MaxAmmo > 0 && Ammo < 1 ) return false;

		if ( lower && Input.Down( "Attack1" ) )
		{
			ToggleLower();
		}

		if ( timeSinceLowered < 0.35f ) return false;

		return true;
	}

	protected override void OnFixedUpdate()
	{
		if ( Input.Pressed( "Flashlight" ) )
		{
			// Cycle through to the next fire mode, wrapping around using modulo
			fireMode = (FireMode)(((int)fireMode + 1) % Enum.GetValues<FireMode>().Length);
		}

		var pressedFire = Input.Pressed( "attack1" );

		if ( Input.Down( "attack1" ) )
		{
			if ( CanShoot() )
			{
				RunAttack();

				if ( pressedFire )
				{
					timeSincePrimaryAttackStarted = 0.0f;
				}
			}
			else
			{
				if ( pressedFire && Ammo < 1 )
				{
					if ( viewmodel.Components.TryGet<SkinnedModelRenderer>( out var vm ) )
					{
						vm.Set( "b_attack_dry", true );
					}
				}
			}
		}

		if ( Input.Pressed( "reload" ) )
		{
			if ( MaxAmmo > 0 && Ammo >= MaxAmmo ) return; // Don't reload if we're full

			// Start the reload sequence
			if ( viewmodel.Components.TryGet<SkinnedModelRenderer>( out var vm ) )
			{
				isReloading = true;

				if ( !SingularReload )
				{
					vm.Set( "b_reload", true );

					Invoke( ReloadTime, () =>
					{
						Ammo = MaxAmmo;
						isReloading = false;
					} );
				}
				else
				{
					vm.Set( "b_reloading", true );
				}

				timeSinceReload = 0;
			}
		}

		if ( isReloading && SingularReload )
		{
			if ( timeSinceReload >= ReloadTime )
			{
				if ( viewmodel.Components.TryGet<SkinnedModelRenderer>( out var vm ) )
				{
					vm.Set( "b_reloading_shell", true );
					timeSinceReload = 0;
					Ammo = Math.Min( Ammo + 1, MaxAmmo );

					if ( Ammo >= MaxAmmo )
					{
						isReloading = false;
						vm.Set( "b_reloading", false );
					}
				}
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
		timeSinceLastShoot = 0;

		Ammo--;

		Vector3 shootPosition = WorldPosition;

		if ( viewmodel.Components.TryGet<SkinnedModelRenderer>( out var vm ) )
		{
			vm.Set( "b_attack", true );

			var controller = Components.Get<PlayerController>( FindMode.InAncestors );
			controller.EyeAngles += new Angles( PitchRecoil.GetValue(), YawRecoil.GetValue(), 0 );

			_ = new Sandbox.CameraNoise.Recoil( RecoilNoiseScale, RecoilNoiseSpeed );

			if ( UseMuzzleFlash )
			{
				var muzzle = vm.GetBoneObject( vm.Model.Bones.GetBone( "muzzle" ) ) ?? vm.GameObject;
				shootPosition = muzzle.WorldPosition;
				GameObject.Clone( "/effects/muzzle.prefab", global::Transform.Zero, muzzle );
			}
		}

		Sound.Play( PrimaryAttackSound, shootPosition );

		ShootBullet();

	}

	void RunAltAttack()
	{
	}

	void ShootBullet()
	{
		var ray = Scene.Camera.WorldTransform.ForwardRay;
		ray.Forward += Vector3.Random * 0.01f;

		var tr = Scene.Trace.Ray( ray, RayDistance )
					.IgnoreGameObjectHierarchy( GameObject.Parent )
					.Run();

		//Sound.Play( shootSound, Transform.Position );

		if ( !tr.Hit || tr.GameObject is null )
			return;

		if ( tr.Surface is not null )
		{
			var prefab = tr.Surface.PrefabCollection.BulletImpact ?? tr.Surface.GetBaseSurface()?.PrefabCollection.BulletImpact;
			if ( prefab is not null )
			{
				prefab?.Clone( new Transform( tr.HitPosition + tr.Normal, Rotation.LookAt( -tr.Normal ) ) );
			}
		}
		else
		{
			GameObject.Clone( "/effects/impact_default.prefab", new Transform( tr.HitPosition + tr.Normal * 2.0f, Rotation.LookAt( tr.Normal ) ) );

			{
				var go = GameObject.Clone( "/effects/decal_bullet_default.prefab" );
				go.WorldTransform = new Transform( tr.HitPosition + tr.Normal * 2.0f, Rotation.LookAt( -tr.Normal, Vector3.Random ), System.Random.Shared.Float( 0.8f, 1.2f ) );
				go.SetParent( tr.GameObject );
			}
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
		modelRender.BodyGroups |= Bodygroups;
		modelRender.RenderOptions.Overlay = true;
		modelRender.RenderOptions.Game = false;

		modelRender.CreateBoneObjects = true;
		modelRender.RenderType = ModelRenderer.ShadowRenderType.Off;

		if ( GraphOverride is not null )
			modelRender.AnimationGraph = GraphOverride;

		{
			var arms = new GameObject();
			arms.Parent = viewmodel;

			var model = arms.Components.Create<SkinnedModelRenderer>();
			model.Model = Model.Load( "models/first_person/first_person_arms.vmdl" );
			model.BoneMergeTarget = modelRender;
			model.RenderOptions.Overlay = true;
			model.RenderOptions.Game = false;
			model.RenderType = ModelRenderer.ShadowRenderType.Off;
		}
	}

	void ICameraSetup.Setup( CameraComponent cc )
	{
		if ( viewmodel is null ) return;

		viewmodel.Tags.Set( "viewer", !BodyRenderer.Tags.Has( "viewer" ) );

		viewmodel.WorldPosition = cc.WorldPosition;
		viewmodel.WorldRotation = cc.WorldRotation;

		viewmodel.LocalRotation *= ViewModelLocalRotation.ToRotation();

		viewmodel.LocalPosition += viewmodel.WorldRotation * ViewModelLocalOffset;

		if ( viewmodel.Components.TryGet<SkinnedModelRenderer>( out var vm ) )
		{
			var bone = vm.GetBoneObject( "camera" );
			var scale = 1;

			cc.LocalPosition += bone.LocalPosition * scale;
			cc.LocalRotation *= bone.LocalRotation * scale;
		}
	}

	void DrawHud( HudPainter hud, Vector2 center )
	{
		if ( IsAiming )
			return;

		var gap = 8;
		var len = 12;
		var w = 2f;
		var color = Color.White.WithAlpha( 0.5f );

		hud.SetBlendMode( BlendMode.Lighten );
		hud.DrawLine( center + Vector2.Left * (len + gap), center + Vector2.Left * gap, w, color );
		hud.DrawLine( center - Vector2.Left * (len + gap), center - Vector2.Left * gap, w, color );
		hud.DrawLine( center + Vector2.Up * (len + gap), center + Vector2.Up * gap, w, color );
		hud.DrawLine( center - Vector2.Up * (len + gap), center - Vector2.Up * gap, w, color );
	}
}
