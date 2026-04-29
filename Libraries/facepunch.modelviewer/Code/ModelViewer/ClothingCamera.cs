using Sandbox;
using Sandbox.Citizen;
using Sandbox.Internal;
using System;
using System.IO;
using System.Numerics;
using static Sandbox.ColorGrading;
using static Sandbox.PhysicsContact;

[Title( "Model Viewer Camera Set Up" )]
[Category( "Model Viewer" )]
[Icon( "camera", "red", "white" )]
[Tint(EditorTint.Yellow)]
public sealed class ClothingCamera : Component
{
	public enum CameraMode
	{
		Orbit,
		Maya,
		FreeCam,
		FPS,
		ThirdPerson
	}

	public enum Poses
	{
		TPose,
		RaisedHands,
		None
	}

	//CameraMode
	[Property][Title( "Camera Mode" )][Group( "Camera Mode" )] public CameraMode Mode { get; set; } = CameraMode.Orbit;
	[Property][Title( "Lock Camera" )][Group( "Camera Mode" )] public bool LockCamera { get; set; } = false;
	[Property][Title( "Camera Focus Object" )][Group( "Camera Mode" )] public GameObject FocusObject { get; set; }

	[Property]
	[Title( "Field of View" )]
	[Range( 0, 180 )]
	[Group( "Camera" )]
	public float Fov { get; set; } = 80.0f;

	[Property]
	[Title( "Near Clip" )]
	[Range( 0, 10 )]
	[Group( "Camera" )]
	public float Near { get; set; } = 0.1f;

	[Property]
	[Title( "Far Clip" )]
	[Range( 0, 10000 )]
	[Group( "Camera" )]
	public float Far { get; set; } = 10000.0f;

	[Property]
	[Title( "Orthographic" )]
	[Group( "Camera" )]
	public bool Ortho { get; set; } = false;

	[Property]
	[Title( "Orthographic Size" )]
	[Range( 0, 1000 )]
	[Group( "Camera" )]
	public float OrthoSize { get; set; } = 1000.0f;


	//Orbit Controls
	[Property][Group( "Orbit Controls" ), ShowIf( "Mode", CameraMode.Orbit )] public float CameraDistance { get; set; } = 200.0f;
	//

	public Vector3 orbitObject { get; set; }
	private float orbitDistance = 200.0f;

	//Maya Controls
	[Property][Title( "Maya Orbit Speed" )][Range( 0, 1 )][Group( "Maya Controls" ),ShowIf( "Mode", CameraMode.Maya )] private float orbitSpeed { get; set; } = 1.0f;
	[Property][Title( "Maya Zoom Speed" )][Range( 0, 1 )][Group( "Maya Controls" ), ShowIf( "Mode", CameraMode.Maya )] private float zoomSpeed { get; set; } = 10.0f;
	[Property][Title( "Maya Pan Speed" )][Range( 0, 10 )][Group( "Maya Controls" ), ShowIf( "Mode", CameraMode.Maya )] private float panSpeed { get; set; } = 10.0f;
	[Property][Title( "Maya Fly Speed" )][Range( 0, 200 )][Group( "Maya Controls" ), ShowIf( "Mode", CameraMode.Maya )] private float MayaFlySpeed { get; set; } = 100.0f;
	//

	//Lazy Susan
	[Property][FeatureEnabled( "LazySusan", Icon = "👩" )] private bool LazySusan { get; set; } = false;

	[Property][Title( "Lazy Susan Speed" )][Range( 0, 2 )][Feature( "LazySusan" )] public float LazySuzzy { get; set; } = 0.50f;
	//

	//World Color
	[Property][Title( "Hide World" )][Group( "World" )] public bool HideWorld { get; set; } = false;
	[Property][Title( "World Color" )][Group( "World" )] public Color WorldColor { get; set; } = Color.White;
	[Property][Title( "Fog Color" )][Group( "World" )] public Color FogColor { get; set; } = Color.White;
	[Property][Title( "Skybox Material" )][Group( "World" )] public Material SkyboxMaterial { get; set; } = Material.Load( "materials/skybox/skybox_day_01.vmat");
	[Property][Title( "Skybox" )][Group( "World" )] SkyBox2D skybox { get; set; }
	[Property][Title( "Fog Start Distance" )][Group( "World" )] public float FogStartDistance { get; set; } = 300.0f;
	[Property][Title( "Fog End Distance" )][Group( "World" )] public float FogEndDistance { get; set; } = 1024.0f;
	[Property][Title( "Fog Falloff Exponent" )][Group( "World" )] public float FogFalloffExponent { get; set; } = 1.0f;
	[Property][Title( "Fog Height" )][Group( "World" )] public float FogHeight { get; set; } = 500.0f;
	[Property][Title( "Fog Verticle Falloff Exponent" )][Group( "World" )] public float FogVerticleFalloffExponent { get; set; } = 2.0f;
	//

	//Post Processing
	[Property, FeatureEnabled( "ColorMapping", Icon = "🎨" )] public bool ColorMapping { get; set; } = true;
	[Property, Feature( "ColorMapping" )] public GradingType ColorGrading { get; set; } = GradingType.None;

	[Property, Feature( "ColorMapping" ), ShowIf( "ColorGrading", GradingType.TemperatureControl )]
	[Range( 1000f, 40000f, 0.01f, true, true )]
	[DefaultValue( 6500f )]
	[Group( "ColorMapping" )] public float Temperature { get; set; } = 0.0f;

	[Property, Feature( "ColorMapping" ), ShowIf( "ColorGrading", GradingType.TemperatureControl )]
	[Range( 0f, 1f, 0.01f, true, true )]
	[DefaultValue( 0f )]
	[Group( "ColorMapping" )] public float Blend { get; set; } = 0.0f;

	[Group( "ColorMapping" ), Feature( "ColorMapping" )]
	[ShowIf( "ColorGrading", GradingType.LUT )]
	[Property]
	public Texture LookupTexture { get; set; } = Texture.White;

	[Group( "ColorMapping" ), Feature( "ColorMapping" )]
	[Property]
	[DefaultValue( ColorSpaceEnum.None )]
	public ColorSpaceEnum ColorSpace { get; set; } = ColorSpaceEnum.None;


	[Group( "ColorMapping" ), Feature( "ColorMapping" )]
	[Property]
	[ShowIf( "ColorSpace", ColorSpaceEnum.RGB )]
	public Curve Rcurve { get; set; } = new Curve( new Curve.Frame( 0f, 0.5f ), new Curve.Frame( 1f, 1f ) );


	[Group( "ColorMapping" ), Feature( "ColorMapping" )]
	[Property]
	[ShowIf( "ColorSpace", ColorSpaceEnum.RGB )]
	public Curve Gcurve { get; set; } = new Curve( new Curve.Frame( 0f, 0.5f ), new Curve.Frame( 1f, 1f ) );


	[Group( "ColorMapping" ), Feature( "ColorMapping" )]
	[Property]
	[ShowIf( "ColorSpace", ColorSpaceEnum.RGB )]
	public Curve Bcurve { get; set; } = new Curve( new Curve.Frame( 0f, 0.5f ), new Curve.Frame( 1f, 1f ) );


	[Group( "ColorMapping" ), Feature( "ColorMapping" )]
	[Property]
	[ShowIf( "ColorSpace", ColorSpaceEnum.HSV )]
	public Curve Hcurve { get; set; } = new Curve( new Curve.Frame( 0f, 0.5f ), new Curve.Frame( 1f, 1f ) );


	[Group( "ColorMapping" ), Feature( "ColorMapping" )]
	[Property]
	[ShowIf( "ColorSpace", ColorSpaceEnum.HSV )]
	public Curve Scurve { get; set; } = new Curve( new Curve.Frame( 0f, 0.5f ), new Curve.Frame( 1f, 1f ) );


	[Group( "ColorMapping" ), Feature( "ColorMapping" )]
	[Property]
	[ShowIf( "ColorSpace", ColorSpaceEnum.HSV )]
	public Curve Vcurve { get; set; } = new Curve( new Curve.Frame( 0f, 0.5f ), new Curve.Frame( 1f, 1f ) );


	[Property, FeatureEnabled( "Bloom", Icon = "💣" )] public bool Bloom { get; set; } = true;
	[Property, Feature( "Bloom" )][Title( "Mode" )] public SceneCamera.BloomAccessor.BloomMode BloomMode { get; set; } = SceneCamera.BloomAccessor.BloomMode.Screen;
	[Property, Feature( "Bloom" ),Range(0,10)] public float BloomStrength { get; set; } = 0.1f;
	[Property, Feature( "Bloom" ), Range( 0, 2 )] public float BloomThreshold { get; set; } = 0.3f;
	[Property, Feature( "Bloom" ), Range( 0, 5 )] public float BloomThresholdWidth { get; set; } = 0.0f;
	[Property, Feature( "Bloom" )] public Curve BloomCurve { get; set; } = new Curve( new Curve.Frame( 0.0f, 0.5f ), new Curve.Frame( 1.0f, 1.0f ) );
	[Property, Feature( "Bloom" )] public Gradient BloomColor { get; set; } = new Gradient( new Gradient.ColorFrame( 0.0f, Color.White ), new Gradient.ColorFrame( 1.0f, Color.White ) );
	//
	[Property, FeatureEnabled( "FilmGrain", Icon = "🌌" )] public bool FilmGrain { get; set; } = true;
	[Property, Feature( "FilmGrain" )] public float FilmGrainIntensity { get; set; } = 0.025f;
	[Property, Feature( "FilmGrain" )] public float FilmGrainResponse { get; set; } = 0.5f;
	//
	[Property, FeatureEnabled( "Vignette", Icon = "🔘" )] public bool Vignette { get; set; } = true;
	[Property, Feature( "Vignette" ), Range( 0, 1 )] public float VignetteIntensity { get; set; } = 0.5f;
	[Property, Feature( "Vignette" ), Range( 0, 1 )] public float VignetteSmoothness { get; set; } = 0.5f;
	[Property, Feature( "Vignette" ), Range( 0, 2 )] public float VignetteRoundness { get; set; } = 1.0f;
	[Property, Feature( "Vignette" )] public Color VignetteColor { get; set; } = Color.Black;
	[Property, Feature( "Vignette" )] public Vector2 VignetteCenter { get; set; } = new Vector2( 0.5f, 0.5f );
	//
	[Property, FeatureEnabled( "Sharpen", Icon = "🔪" )] public bool Sharpen { get; set; } = true;
	[Property, Feature( "Sharpen" ), Range( 0, 5 )] public float SharpenIntensity { get; set; } = 0.25f;
	//
	[Property, FeatureEnabled( "DepthOfField", Icon = "🔭" )] public bool DepthOfField { get; set; } = false;
	[Property, Feature( "DepthOfField" )][Range( 0, 1000 )] public float DepthOfFieldFocusDistance { get; set; } = 200.0f;
	[Property, Feature( "DepthOfField" )][Range( 0, 100 )] public float DepthOfFieldBlurSize { get; set; } = 0.0f;
	[Property, Feature( "DepthOfField" )] public bool DepthOfFieldFrontBlur { get; set; } = false;
	[Property, Feature( "DepthOfField" )] public bool DepthOfFieldBackBlur { get; set; } = false;
	//
	[Property, FeatureEnabled( "ChromaticAberration", Icon = "🏳️‍🌈" )] public bool ChromaticAberration { get; set; } = false;
	[Property, Feature( "ChromaticAberration" )] public float ChromaticAberrationIntensity { get; set; } = 0.1f;
	[Property, Feature( "ChromaticAberration" )] public Vector3 ChromaticAberrationOffset { get; set; } = new Vector3( 4.0f, 6.0f, 0.0f );
	//

	//
	[Property, FeatureEnabled( "Animation", Icon = "🏃‍" )] public bool Animation { get; set; } = true;
	[Property, Feature( "Animation" )][Range( 0, 20 )] public int FacePose { get; set; } = 0;
	[Property, Feature( "Animation" )][Range( 0, 20 )] public bool MenuIdle { get; set; } = false;
	[Property, Feature( "Animation" )][Range( -250, 250 )] public Vector3 Move { get; set; } = 0;
	//

	//
	[Property, FeatureEnabled( "IK", Icon = "🕺" )] public bool IK { get; set; } = true;
	[Property, Feature( "IK" )] public Poses IKPose { get; set; } = Poses.None;
	[Property, Feature( "IK" )] public Vector3 LeftHandPosition { get; set; } = new Vector3( 0.0f, 0.0f, 0.0f );
	[Property, Feature( "IK" )] public Rotation LeftHandRotation { get; set; } = Rotation.Identity;
	[Property, Feature( "IK" )] public Vector3 RightHandPosition { get; set; } = new Vector3( 0.0f, 0.0f, 0.0f );
	[Property, Feature( "IK" )] public Rotation RightHandRotation { get; set; } = Rotation.Identity;
	[Property, Feature( "IK" )] public Vector3 LeftFootPosition { get; set; } = new Vector3( 0.0f, 0.0f, 0.0f );
	[Property, Feature( "IK" )] public Rotation LeftFootRotation { get; set; } = Rotation.Identity;
	[Property, Feature( "IK" )] public Vector3 RightFootPosition { get; set; } = new Vector3( 0.0f, 0.0f, 0.0f );
	[Property, Feature( "IK" )] public Rotation RightFootRotation { get; set; } = Rotation.Identity;
	[Property, Feature( "IK" )] public Rotation HeadLook { get; set; } = Rotation.Identity;
	[Property, Feature( "IK" )] public Rotation EyeLook { get; set; } = Rotation.Identity;
	[Property, Feature( "IK" )] public Rotation BodyLook { get; set; } = Rotation.Identity;


	//
	[Property, FeatureEnabled( "AnimatedViewModel", Icon = "🫸" )] public bool AnimatedViewModel { get; set; } = false;
	[Property, Feature( "AnimatedViewModel" )] public Vector3 AnimatedViewModelOffset { get; set; } = new Vector3( 15.0f, -5.0f, -8.0f );
	[Property, Feature( "AnimatedViewModel" )] public Rotation AnimatedViewModelRotation { get; set; } = new Rotation( 1.0f, 0.0f, 0.0f, 0.0f );
	//

	//
	[Property, FeatureEnabled( "StaticViewModel", Icon = "🤳🏼" )] public bool StaticViewModel { get; set; } = false;
	[Property, Feature( "StaticViewModel" )] public Vector3 StaticiewModelOffset { get; set; } = new Vector3( 15.0f, -5.0f, -8.0f );
	[Property, Feature( "StaticViewModel" )] public Rotation StaticViewModelRotation { get; set; } = new Rotation( 1.0f, 0.0f, 0.0f, 0.0f );
	//

	/*
	[Property, ToggleGroup( "PlayerController", Label = "Player Controller" )] public bool PlayerController { get; set; } = false;
	[Property, ToggleGroup( "PlayerController", Label = "Maya Camera Follow" )] public bool MayaFollowPlayer { get; set; } = true;
	[Property, ToggleGroup( "PlayerController", Label = "Z Offset" )][Range( 0, 72 )] public float Zoffset { get; set; } = 48;
	*/
	//
	public bool PlayerController { get; set; } = false;
	public bool MayaFollowPlayer { get; set; } = false;
	public float Zoffset { get; set; } = 48;
	//

	//
	[Property, FeatureEnabled( "Don't worry about these", Icon = "🚫" )] public bool DontWorry { get; set; } = false;
	[Feature( "Don't worry about these" )]
	[Property] GameObject CameraObject { get; set; }
	[Feature( "Don't worry about these" )]
	[Property] GameObject CameraOrbit { get; set; }
	[Feature( "Don't worry about these" )]
	[Property] GameObject CharacterObject { get; set; }
	[Feature( "Don't worry about these" )]
	[Property] GameObject LightingRig { get; set; }
	[Feature( "Don't worry about these" )]
	[Property] public GameObject ViewModelObject { get; set; }
	[Feature( "Don't worry about these" )]
	[Property] GameObject StaticViewModelObject { get; set; }
	[Feature( "Don't worry about these" )]
	[Property] GameObject PlayerControllerObject { get; set; }

	[Feature( "Don't worry about these" )]
	[Property] IkMover LeftHand { get; set; }
	[Feature( "Don't worry about these" )]
	[Property] IkMover RightHand { get; set; }
	[Feature( "Don't worry about these" )]
	[Property] IkMover LeftFoot { get; set; }
	[Feature( "Don't worry about these" )]
	[Property] IkMover RightFoot { get; set; }
	[Feature( "Don't worry about these" )]
	[Property]
	public MVCitizenAnimation Animator { get; set; }
	[Feature( "Don't worry about these" )]
	[Property]
	public ModelRenderer WorldFloor { get; set; }
	[Feature( "Don't worry about these" )]
	[Property]
	public ModelRenderer WorldCeiling { get; set; }
	[Feature( "Don't worry about these" )]
	[Property]
	public GradientFog WorldFog { get; set; }

	[Feature( "Don't worry about these" )]
	[Property]
	public ColorGrading Grading { get; set; }

	[Feature( "Don't worry about these" )]
	[Property]
	public GameObject WorldObject { get; set; }
	//

	private float MayaSpeed { get; set; } = 100.0f;

	private Vector2 cameraAngles;

	Vector3 wishDir = default;

	public Angles EyeAngles;

	public float ZOffset;

	public float FovScale = 50.0f;

	public float CharacterRotation = 180.0f;

	private bool isInFlyMode = false;
	public float flySpeed = 50.0f;

	private float targetCameraDistance;
	private float targetZOffset;
	private float targetFovScale;
	private float targetCharacterRotation;

	private Bloom bloomCompo;
	private FilmGrain flimgrainCompo;
	private Vignette vigneCompo;
	private Sharpen sharpenComp;
	private DepthOfField dofCompo;
	private ChromaticAberration chromCompo;

	public float EyeHeight { get; set; } = 64.0f;
	public Vector3 WishVelocity { get; private set; }
	public float JumpForce = 1200f; // The force of the jump
	public bool isGrounded = false;
	private Vector3 gravity { get; set; } = new Vector3( 0, 0, 800 );

	public ClothingCamera()
	{
		targetCameraDistance = CameraDistance;
		targetZOffset = ZOffset;
		targetFovScale = FovScale;
		targetCharacterRotation = CharacterRotation;
	}

	protected override void OnStart()
	{
		base.OnStart();
		orbitObject = CharacterObject.WorldPosition;

		ViewModelObject.Parent = CameraObject;
		StaticViewModelObject.Parent = CameraObject;
		CameraObject.WorldPosition = CharacterObject.WorldPosition + CameraObject.WorldRotation.Backward * orbitDistance;

		skybox = Scene.Components.Get<SkyBox2D>(FindMode.EverythingInSelfAndDescendants);
	}

	public void FakePlayerMovement()
	{

		// Clamp pitch to prevent over-rotation (optional but useful to prevent full flips)
		EyeAngles.pitch = Math.Clamp( EyeAngles.pitch, -89.9f, 89.9f );

		CameraObject.WorldRotation = EyeAngles.ToRotation();

		var rot = EyeAngles.ToRotation();

		WishVelocity = 0;

		if ( Input.Down( "Forward" ) ) WishVelocity += rot.Forward;
		if ( Input.Down( "Backward" ) ) WishVelocity += rot.Backward;
		if ( Input.Down( "Left" ) ) WishVelocity += rot.Left;
		if ( Input.Down( "Right" ) ) WishVelocity += rot.Right;

		WishVelocity = WishVelocity.WithZ( 0 );

		if ( !WishVelocity.IsNearZeroLength ) WishVelocity = WishVelocity.Normal;

		if ( Input.Down( "Run" ) ) WishVelocity *= 320.0f;
		else WishVelocity *= 70.0f;

		// Jump logic
		if ( Input.Down( "Jump" ) && isGrounded )
		{
			//Log.Info( "Jump" );

			float flGroundFactor = 1.0f;
			float flMul = 268.3281572999747f * 10.2f;
			Punch( Vector3.Up * flMul * flGroundFactor );

			var anim = ViewModelObject.Children[0].Components.Get<SkinnedModelRenderer>();
		}
	}

	public void Punch( in Vector3 amount )
	{
		WishVelocity += amount;
	}

	public void FakeFPSPhys()
	{

		// Apply gravity if the player is in the air
		if ( !isGrounded )
		{
			// Apply gravity to vertical velocity
			WishVelocity -= gravity * Time.Delta * 5.5f;

			// Check if the player has reached the ground
			if ( CameraObject.WorldPosition.z <= EyeHeight )
			{
				// Snap back to ground level and reset jump state
				CameraObject.WorldPosition = CameraObject.WorldPosition.WithZ( 0 + EyeHeight );
				//WishVelocity = WishVelocity.WithZ(0); // Reset vertical velocity
			}
		}

		// Apply the calculated WishVelocity to move the camera
		CameraObject.WorldPosition = CameraObject.WorldPosition + WishVelocity * RealTime.Delta;
	}

	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();

		if ( Mode == CameraMode.FPS )
		{
			FakeFPSPhys();
			FakePlayerMovement();
			return;
		}
	}

	public void SetIK()
	{
		if ( IKPose == Poses.None )
		{
			LeftHand.Offset = LeftHandPosition;
			RightHand.Offset = RightHandPosition;
			LeftFoot.Offset = LeftFootPosition;
			RightFoot.Offset = RightFootPosition;
			LeftHand.RotationOffset = LeftHandRotation;
			RightHand.RotationOffset = RightHandRotation;
			LeftFoot.RotationOffset = LeftFootRotation;
			RightFoot.RotationOffset = RightFootRotation;
		}
		else if ( IKPose == Poses.TPose )
		{
			LeftHand.Offset = new Vector3( 0, -32, 50 );
			RightHand.Offset = new Vector3( 0, 32, 50 );
			LeftFoot.Offset = new Vector3( 0, -4, 4 );
			RightFoot.Offset = new Vector3( 0, 4, 4 );
			LeftHand.RotationOffset = new Rotation( 0.70f, -0.70f, 0.0f, -0.0f );
			RightHand.RotationOffset = new Rotation( 0, 0, 0.70f, 0.70f );
			LeftFoot.RotationOffset = new Rotation( -0.2705985f, 0.6532815f, 0.6532814f, 0.2705978f );
			RightFoot.RotationOffset = new Rotation( -0.2705985f, 0.6532815f, 0.6532814f, 0.2705978f );
		}
		else if ( IKPose == Poses.RaisedHands )
		{

			LeftHand.Offset = new Vector3( 0, -32, 150 );
			RightHand.Offset = new Vector3( 0, 23, 150 );
			LeftFoot.Offset = new Vector3( 0, -4, 4 );
			RightFoot.Offset = new Vector3( 0, 4, 4 );
			LeftHand.RotationOffset = new Rotation( 0.6418805f, -0.3497548f, 0.6190226f, 0.2871793f );
			RightHand.RotationOffset = new Rotation( -0.3726256f, 0.6093453f, -0.301973f, -0.6313959f );
			LeftFoot.RotationOffset = new Rotation( -0.2705985f, 0.653281f, 0.653281f, 0.2705978f );
			RightFoot.RotationOffset = new Rotation( -0.2705985f, 0.6532815f, 0.653281f, 0.2705978f );
		}

		var animmod = CharacterObject.Components.Get<SkinnedModelRenderer>();
		animmod.Set( "aim_head", HeadLook.Forward * 10 );
		animmod.Set( "aim_eyes", EyeLook.Forward * 50 );
		animmod.Set( "aim_body", BodyLook.Forward * 10 );
	}


	public void WorldStuff()
	{
		WorldCeiling.Tint = WorldColor;
		WorldFloor.Tint = WorldColor;
		if ( skybox.IsValid() )
		{
			skybox.SkyMaterial = SkyboxMaterial;
		}
		WorldFog.Color = FogColor;
		WorldFog.StartDistance = FogStartDistance;
		WorldFog.EndDistance = FogEndDistance;
		WorldFog.FalloffExponent = FogFalloffExponent;
		WorldFog.Height = FogHeight;
		WorldFog.VerticalFalloffExponent = FogVerticleFalloffExponent;
	}


	protected override void OnUpdate()
	{
		//Update camera
		UpdateCamera();
		RotateLightingRig();
		WorldStuff();

		// Eye input
		EyeAngles.pitch += Input.MouseDelta.y * 0.1f;
		EyeAngles.yaw -= Input.MouseDelta.x * 0.1f;
		EyeAngles.roll = 0;

		if(WorldObject != null)
		{
			WorldObject.Enabled = !HideWorld;
		}

		if ( Animator.IsValid() )
		{
			if ( IK )
			{
				SetIK();
				Animator.IkLeftFoot = LeftFoot.GameObject.Children[0];
				Animator.IkRightFoot = RightFoot.GameObject.Children[0];
				Animator.IkLeftHand = LeftHand.GameObject.Children[0];
				Animator.IkRightHand = RightHand.GameObject.Children[0];
			}
			else
			{
				Animator.IkLeftFoot = null;
				Animator.IkRightFoot = null;
				Animator.IkLeftHand = null;
				Animator.IkRightHand = null;
			}
			if ( Animation )
			{
				Animator.FacesOverride = FacePose;
				Animator.WithVelocity( Move );
				Animator.SpecialMenu( MenuIdle );
			}
		}

		if ( PlayerController )
		{
			PlayerControllerObject.Enabled = true;
		}
		else
		{
			PlayerControllerObject.Enabled = false;
		}


		if ( StaticViewModel )
		{
			AnimatedViewModel = false;
			StaticViewModelObject.Enabled = true;
			StaticViewModelObject.Transform.LocalPosition = StaticiewModelOffset;
			StaticViewModelObject.Transform.LocalRotation = StaticViewModelRotation;
		}
		else
		{
			StaticViewModelObject.Enabled = false;
		}

		if ( AnimatedViewModel )
		{
			StaticViewModel = false;
			ViewModelObject.Enabled = true;
			ViewModelObject.Children[0].Enabled = true;
			ViewModelObject.Transform.LocalPosition = AnimatedViewModelOffset;
			ViewModelObject.Transform.LocalRotation = AnimatedViewModelRotation;
		}
		else
		{
			ViewModelObject.Enabled = false;
			ViewModelObject.Children[0].Enabled = false;
		}

		if ( Input.Pressed( "View" ) )
		{
			LazySusan = !LazySusan;
		}

		if ( LazySusan )
		{
			CharacterRotation += LazySuzzy;
		}

		var tr = Scene.PhysicsWorld.Trace.Ray( CameraObject.WorldPosition, CameraObject.WorldPosition + Vector3.Down * 100 )
			.Run();

		if ( tr.Distance <= EyeHeight )
		{
			isGrounded = true;
		}
		else
		{
			isGrounded = false;
		}

		CharacterObject.WorldRotation = Rotation.From( 0, CharacterRotation, 0 );

		if ( Input.Pressed( "slot1" ) )
		{
			Mode = CameraMode.Orbit;
		}
		if ( Input.Pressed( "slot2" ) )
		{
			Mode = CameraMode.Maya;
		}
		if ( Input.Pressed( "slot3" ) )
		{
			Mode = CameraMode.FreeCam;
		}

		if ( PlayerController )
		{
			if ( Mode == CameraMode.ThirdPerson )
			{
				CameraObject.Enabled = false;
				var tps = PlayerControllerObject.Components.Get<CameraComponent>(FindMode.EverythingInSelf );
				tps.GameObject.Enabled = true;
			}
			else
			{
				var tps = PlayerControllerObject.Components.Get<CameraComponent>( FindMode.EverythingInSelf );
				tps.GameObject.Enabled = false;
				CameraObject.Enabled = true;
			}
		}

		if ( Mode == CameraMode.FPS )
		{
			return;
		}

		if ( Mode == CameraMode.Orbit )
		{
			OrbitCamera();
			return;
		}

		if ( Mode == CameraMode.Maya )
		{
			MayaCamera();
			return;
		}

		if ( Mode == CameraMode.FreeCam )
		{
			HandleFlyCameraMovement();
			return;
		}
	}

	private float zoffet = 0;
	private void MayaCamera()
	{
		if ( !LockCamera )
		{
			// Simulate how the Maya camera works
			var camera = CameraObject.Components.Get<CameraComponent>( FindMode.EverythingInSelf );
			if ( camera is not null )
			{
				float x = Input.MouseDelta.x;
				float y = Input.MouseDelta.y;

				// Check if both "attack2" and "walk" are held
				if ( Input.Down( "attack1" ) && Input.Down( "walk" ) )
				{
					// Rotate the camera around the orbitObject
					cameraAngles += new Vector2( y * orbitSpeed, x * orbitSpeed );

					// Limit pitch angle to prevent camera flipping
					cameraAngles.x = Math.Clamp( cameraAngles.x, -89.9f, 89.9f );

					// Convert cameraAngles to a rotation
					CameraObject.WorldRotation = Rotation.From( cameraAngles.x, -cameraAngles.y, 0 );

					// Calculate the new camera position based on the orbit distance
					var newCameraPosition = orbitObject + CameraObject.WorldRotation.Backward * orbitDistance;

					// Set the camera position
					CameraObject.WorldPosition = newCameraPosition;
				}
				else if ( Input.Down( "attack2" ) && Input.Down( "walk" ) )
				{
					// Calculate zoom speed for zooming in/out
					var currentZoomSpeed = Math.Clamp( zoomSpeed * (orbitDistance / 50), 0.1f, 2.0f );

					// Zoom the camera in/out
					CameraObject.WorldPosition += CameraObject.WorldRotation.Backward * (y * -1f * currentZoomSpeed);
					orbitDistance = CameraObject.WorldPosition.Distance( orbitObject );
				}
				else if ( Input.Down( "attack3" ) && Input.Down( "walk" ) )
				{

					var translateX = CameraObject.WorldRotation.Right * (-x * panSpeed * RealTime.Delta);
					var translateY = CameraObject.WorldRotation.Up * (y * panSpeed * RealTime.Delta);

					// Move the camera in the local coordinates
					CameraObject.WorldPosition += translateX;
					CameraObject.WorldPosition += translateY;

					// Move the orbitObject with the same values, along the camera's axes.
					orbitObject += translateX;
					orbitObject += translateY;
				}
				else
				{
					var currentZoomSpeed = Math.Clamp( zoomSpeed * (orbitDistance / 50), 0.1f, 2.0f );

					// Zoom the camera in/out
					CameraObject.WorldPosition += CameraObject.WorldRotation.Backward * (Input.MouseWheel.y * -1f * currentZoomSpeed);

					// Handle regular camera movement if "attack2" or "walk" are not both held
					wishDir = Vector3.Zero;

					if ( !MayaFollowPlayer )
					{

						if ( Input.Down( "Forward" ) ) wishDir.x = 1;
						else if ( Input.Down( "Backward" ) ) wishDir.x = -1;

						if ( Input.Down( "Left" ) ) wishDir.y = 1;
						else if ( Input.Down( "Right" ) ) wishDir.y = -1;

						wishDir = wishDir.Normal;

						MayaSpeed = Input.Down( "run" ) ? MayaFlySpeed : MayaFlySpeed / 2;

						if ( wishDir.Length > 0 )
						{
							CameraObject.WorldPosition += (CameraObject.WorldRotation * wishDir).Normal * MayaSpeed * Time.Delta;
						}
					}

					if ( Input.Pressed( "flashlight" ) )
					{
						if ( FocusObject is not null )
						{
							CameraObject.WorldPosition = FocusObject.WorldPosition + FocusObject.WorldRotation.Forward * orbitDistance;
							CameraObject.WorldRotation = Rotation.LookAt( FocusObject.WorldPosition - CameraObject.WorldPosition );
						}
						else
						{
							CameraObject.WorldPosition = Vector3.Zero + CameraObject.WorldRotation.Forward * orbitDistance;
							CameraObject.WorldRotation = Rotation.LookAt( Vector3.Zero - CameraObject.WorldPosition );
						}

					}

					if ( MayaFollowPlayer )
					{
						orbitObject = PlayerControllerObject.WorldPosition + new Vector3( 0, 0, Zoffset );
						// Calculate the new camera position based on the orbit distance
						var newCameraPosition = orbitObject + CameraObject.WorldRotation.Backward * orbitDistance;

						// Set the camera position
						CameraObject.WorldPosition = newCameraPosition;
					}
					else
					{
						orbitObject = CameraObject.WorldPosition + CameraObject.WorldRotation.Forward * orbitDistance;
					}

				}
			}
		}
		if ( Input.Down( "walk" ) )
		{
			//Gizmo.Draw.Sprite( orbitObject, 1, "materials/gizmo/envmap.png" );
		}
	}

	private void OrbitCamera()
	{
		if ( !LockCamera )
		{
			if ( !Input.Down( "Walk" ) && !Input.Down( "Run" ) && !Input.Down( "Duck" ) )
			{
				targetCameraDistance = Math.Clamp( targetCameraDistance + Input.MouseWheel.y * -5, 50, 500 );
				CameraDistance = Lerp( CameraDistance, targetCameraDistance, 0.1f );
			}
			if ( Input.Down( "Walk" ) )
			{
				targetZOffset = Math.Clamp( targetZOffset + Input.MouseWheel.y * -2, -100, 100 );
				ZOffset = Lerp( ZOffset, targetZOffset, 0.1f );
			}
			/*
			if ( Input.Down( "Run" ) )
			{
				targetFovScale = Math.Clamp( targetFovScale + Input.MouseWheel * -5, 10, 100 );
				FovScale = Lerp( FovScale, targetFovScale, 0.1f );
			}
			*/
			if ( Input.Down( "Duck" ) )
			{
				targetCharacterRotation = targetCharacterRotation + Input.MouseWheel.y * -5;
				CharacterRotation = Lerp( CharacterRotation, targetCharacterRotation, 0.1f );
			}

			// Update camera position
			var camera = CameraObject.Components.Get<CameraComponent>( FindMode.EverythingInSelf );
			if ( camera is not null )
			{

				var camPos = CameraOrbit.WorldPosition - EyeAngles.ToRotation().Forward * CameraDistance;
				camPos.z += ZOffset;

				camera.WorldPosition = camPos;
				camera.WorldRotation = EyeAngles.ToRotation();

			}
		}
	}

	private void HandleFlyCameraMovement()
	{
		var direction = Vector3.Zero;

		if ( Input.Down( "Forward" ) )
			direction += CameraObject.WorldRotation.Forward;
		if ( Input.Down( "Backward" ) )
			direction -= CameraObject.WorldRotation.Forward;
		if ( Input.Down( "Left" ) )
			direction -= CameraObject.WorldRotation.Right;
		if ( Input.Down( "Right" ) )
			direction += CameraObject.WorldRotation.Right;
		if ( Input.Down( "Jump" ) && Mode == CameraMode.FreeCam ) // Assuming "Space" is for moving upwards
			direction += Vector3.Up;
		if ( Input.Down( "Duck" ) ) // Assuming "LeftShift" is for moving downwards
			direction -= Vector3.Up;

		flySpeed = Input.Down( "Run" ) ? 150.0f : 50.0f;

		// Normalize the direction to ensure consistent speed in diagonal movement
		float magnitude = direction.Length;
		if ( magnitude > 0 )
		{
			direction.x /= magnitude;
			direction.y /= magnitude;
			direction.z /= magnitude;
		}

		// Update the position based on the direction
		CameraObject.WorldPosition += direction * flySpeed * Time.Delta; // Assuming Time.Delta is the time since the last frame

		// Adjusting view direction based on mouse movement
		EyeAngles.pitch += Input.MouseDelta.y * 0.03f;
		EyeAngles.yaw -= Input.MouseDelta.x * 0.03f;

		// Clamp pitch to prevent over-rotation (optional but useful to prevent full flips)
		EyeAngles.pitch = Math.Clamp( EyeAngles.pitch, -89.9f, 89.9f );

		// Convert EyeAngles to a rotation and set to the camera
		CameraObject.WorldRotation = EyeAngles.ToRotation();
	}

	public static float Lerp( float a, float b, float t )
	{
		return a + t * (b - a);
	}

	private float lightRigRotation { get; set; }

	public void RotateLightingRig()
	{
		float x = Input.MouseDelta.x;

		if ( Input.Down( "run" ) && Input.Down( "attack2" ) )
		{
			lightRigRotation += x;
			LightingRig.WorldRotation = Rotation.From( 0, lightRigRotation, 0 );
		}



	}

	public void UpdateCamera()
	{
		var cam = CameraObject.Components.Get<CameraComponent>( FindMode.EverythingInSelf );

		if ( cam is not null )
		{
			cam.FieldOfView = Fov;
			cam.ZNear = Near;
			cam.ZFar = Far;
			cam.Orthographic = Ortho;
			cam.OrthographicHeight = OrthoSize;

			bloomCompo = CameraObject.Components.Get<Bloom>( FindMode.EverythingInSelf );
			bloomCompo.Enabled = Bloom;
			bloomCompo.Mode = BloomMode;
			bloomCompo.Strength = BloomStrength;
			bloomCompo.Threshold = BloomThreshold;

			flimgrainCompo = CameraObject.Components.Get<FilmGrain>( FindMode.EverythingInSelf );
			flimgrainCompo.Enabled = FilmGrain;
			flimgrainCompo.Intensity = FilmGrainIntensity;
			flimgrainCompo.Response = FilmGrainResponse;

			vigneCompo = CameraObject.Components.Get<Vignette>( FindMode.EverythingInSelf );
			vigneCompo.Enabled = Vignette;
			vigneCompo.Intensity = VignetteIntensity;
			vigneCompo.Smoothness = VignetteSmoothness;
			vigneCompo.Roundness = VignetteRoundness;
			vigneCompo.Color = VignetteColor;
			vigneCompo.Center = VignetteCenter;

			sharpenComp = CameraObject.Components.Get<Sharpen>( FindMode.EverythingInSelf );
			sharpenComp.Enabled = Sharpen;
			sharpenComp.Scale = SharpenIntensity;

			dofCompo = CameraObject.Components.Get<DepthOfField>( FindMode.EverythingInSelf );
			dofCompo.Enabled = DepthOfField;
			dofCompo.FocalDistance = DepthOfFieldFocusDistance;
			dofCompo.BlurSize = DepthOfFieldBlurSize;
			dofCompo.FrontBlur = DepthOfFieldFrontBlur;
			dofCompo.BackBlur = DepthOfFieldBackBlur;

			chromCompo = CameraObject.Components.Get<ChromaticAberration>( FindMode.EverythingInSelf );
			chromCompo.Enabled = ChromaticAberration;
			chromCompo.Scale = ChromaticAberrationIntensity;
			chromCompo.Offset = ChromaticAberrationOffset;

			Grading = CameraObject.Components.Get<ColorGrading>( FindMode.EverythingInSelf );
			Grading.Enabled = ColorMapping;
			Grading.GradingMethod = ColorGrading;
			Grading.ColorTempK = Temperature;
			Grading.BlendFactor = Blend;
			Grading.LookupTexture = LookupTexture;

			Grading.ColorSpace = ColorSpace;

		}
	}
}
