using Sandbox;
using System;

[Title( "Camera Controller" )]
[Category( "Model Viewer" )]
[Icon( "videocam", "red", "white" )]
[Tint( EditorTint.Yellow )]
public sealed class CameraController : Component
{
	public enum CameraMode
	{
		Orbit,
		Maya,
		FreeCam
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

	[Property]
	[Group( "Camera" )]
	public bool UseViewModelCamera { get; set; } = false;

	//Orbit Controls
	[Property][Group( "Orbit Controls" ), ShowIf( "Mode", CameraMode.Orbit )] public float CameraDistance { get; set; } = 200.0f;
	//

	public Vector3 orbitObject { get; set; }
	private float orbitDistance = 200.0f;

	//Maya Controls
	[Property][Title( "Maya Orbit Speed" )][Range( 0, 1 )][Group( "Maya Controls" ), ShowIf( "Mode", CameraMode.Maya )] private float orbitSpeed { get; set; } = 1.0f;
	[Property][Title( "Maya Zoom Speed" )][Range( 0, 1 )][Group( "Maya Controls" ), ShowIf( "Mode", CameraMode.Maya )] private float zoomSpeed { get; set; } = 10.0f;
	[Property][Title( "Maya Pan Speed" )][Range( 0, 10 )][Group( "Maya Controls" ), ShowIf( "Mode", CameraMode.Maya )] private float panSpeed { get; set; } = 10.0f;
	[Property][Title( "Maya Fly Speed" )][Range( 0, 200 )][Group( "Maya Controls" ), ShowIf( "Mode", CameraMode.Maya )] private float MayaFlySpeed { get; set; } = 100.0f;
	//

	//Lighting Rig Rotation
	[Property, Group( "LightingRigRotation" )][Title( "Rotation Speed" )][Range( 0, 5 )] public float LightingRigRotationSpeed { get; set; } = 1.0f;
	[Property, Group( "LightingRigRotation" )] public GameObject LightingRig { get; set; }
	//

	//References
	public GameObject CameraObject { get; set; }
	[Property][Group( "References" )] public GameObject ViewModelCameraObject { get; set; }
	[Property][Group( "References" )] public GameObject CameraOrbit { get; set; }
	//

	private float MayaSpeed { get; set; } = 100.0f;

	private Vector2 cameraAngles;

	Vector3 wishDir = default;

	public Angles EyeAngles;

	public float ZOffset;

	public float FovScale = 50.0f;

	public float CharacterRotation = 180.0f;

	public float flySpeed = 50.0f;

	private float targetCameraDistance;
	private float targetZOffset;
	private float targetFovScale;
	private float targetCharacterRotation;
	private float lightRigRotation;

	public CameraController()
	{
		targetCameraDistance = CameraDistance;
		targetZOffset = ZOffset;
		targetFovScale = FovScale;
		targetCharacterRotation = CharacterRotation;
	}

	protected override void OnStart()
	{
		base.OnStart();
		CameraObject = Scene.Camera.GameObject;

		orbitObject = Vector3.Zero;
		CameraObject.WorldPosition = Vector3.Zero + CameraObject.WorldRotation.Backward * orbitDistance;
	}

	protected override void OnUpdate()
	{
		UpdateCameraSettings();
		RotateLightingRig();

		// Eye input
		EyeAngles.pitch += Input.MouseDelta.y * 0.1f;
		EyeAngles.yaw -= Input.MouseDelta.x * 0.1f;
		EyeAngles.roll = 0;

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

	private void MayaCamera()
	{
		if ( !LockCamera )
		{
			var camera = CameraObject.Components.Get<CameraComponent>( FindMode.EverythingInSelf );
			if ( camera is not null )
			{
				float x = Input.MouseDelta.x;
				float y = Input.MouseDelta.y;

				if ( Input.Down( "attack1" ) && Input.Down( "walk" ) )
				{
					cameraAngles += new Vector2( y * orbitSpeed, x * orbitSpeed );
					cameraAngles.x = Math.Clamp( cameraAngles.x, -89.9f, 89.9f );
					CameraObject.WorldRotation = Rotation.From( cameraAngles.x, -cameraAngles.y, 0 );
					var newCameraPosition = orbitObject + CameraObject.WorldRotation.Backward * orbitDistance;
					CameraObject.WorldPosition = newCameraPosition;
				}
				else if ( Input.Down( "attack2" ) && Input.Down( "walk" ) )
				{
					var currentZoomSpeed = Math.Clamp( zoomSpeed * (orbitDistance / 50), 0.1f, 2.0f );
					CameraObject.WorldPosition += CameraObject.WorldRotation.Backward * (y * -1f * currentZoomSpeed);
					orbitDistance = CameraObject.WorldPosition.Distance( orbitObject );
				}
				else if ( Input.Down( "attack3" ) && Input.Down( "walk" ) )
				{
					var translateX = CameraObject.WorldRotation.Right * (-x * panSpeed * RealTime.Delta);
					var translateY = CameraObject.WorldRotation.Up * (y * panSpeed * RealTime.Delta);
					CameraObject.WorldPosition += translateX;
					CameraObject.WorldPosition += translateY;
					orbitObject += translateX;
					orbitObject += translateY;
				}
				else
				{
					var currentZoomSpeed = Math.Clamp( zoomSpeed * (orbitDistance / 50), 0.1f, 2.0f );
					CameraObject.WorldPosition += CameraObject.WorldRotation.Backward * (Input.MouseWheel.y * -1f * currentZoomSpeed);

					wishDir = Vector3.Zero;

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

					orbitObject = CameraObject.WorldPosition + CameraObject.WorldRotation.Forward * orbitDistance;
				}
			}
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
			if ( Input.Down( "Duck" ) )
			{
				targetCharacterRotation = targetCharacterRotation + Input.MouseWheel.y * -5;
				CharacterRotation = Lerp( CharacterRotation, targetCharacterRotation, 0.1f );
			}

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
		if ( Input.Down( "Jump" ) )
			direction += Vector3.Up;
		if ( Input.Down( "Duck" ) )
			direction -= Vector3.Up;

		flySpeed = Input.Down( "Run" ) ? 150.0f : 50.0f;

		float magnitude = direction.Length;
		if ( magnitude > 0 )
		{
			direction.x /= magnitude;
			direction.y /= magnitude;
			direction.z /= magnitude;
		}

		CameraObject.WorldPosition += direction * flySpeed * Time.Delta;

		EyeAngles.pitch += Input.MouseDelta.y * 0.03f;
		EyeAngles.yaw -= Input.MouseDelta.x * 0.03f;

		EyeAngles.pitch = Math.Clamp( EyeAngles.pitch, -89.9f, 89.9f );

		CameraObject.WorldRotation = EyeAngles.ToRotation();
	}

	public void RotateLightingRig()
	{
		if ( !LightingRig.IsValid() )
			return;

		float x = Input.MouseDelta.x;

		if ( Input.Down( "run" ) && Input.Down( "attack2" ) )
		{
			lightRigRotation += x * LightingRigRotationSpeed;
			LightingRig.WorldRotation = Rotation.From( 0, lightRigRotation, 0 );
		}
	}

	public void UpdateCameraSettings()
	{
		if ( !CameraObject.IsValid() )
		{
			CameraObject = Scene.Camera.GameObject;
		}

		var cam = CameraObject.GetComponent<CameraComponent>( true );

		if ( ViewModelCameraObject.IsValid() )
		{
			var vm = ViewModelCameraObject.GetComponent<CameraComponent>( true );

			if ( Input.Pressed( "voice" ) ) UseViewModelCamera = !UseViewModelCamera;

			if ( vm.IsValid() && UseViewModelCamera )
			{
				vm.Enabled = true;
				cam.Enabled = false;
				return;
			}
			else if ( vm.IsValid() && !UseViewModelCamera )
			{
				vm.Enabled = false;
				cam.Enabled = true;
			}
		}

		if ( cam is not null )
		{
			cam.FieldOfView = Fov;
			cam.ZNear = Near;
			cam.ZFar = Far;
			cam.Orthographic = Ortho;
			cam.OrthographicHeight = OrthoSize;
		}
	}

	public static float Lerp( float a, float b, float t )
	{
		return a + t * (b - a);
	}
}
