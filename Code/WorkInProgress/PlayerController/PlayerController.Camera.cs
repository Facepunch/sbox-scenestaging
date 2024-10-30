namespace Sandbox;

public sealed partial class PlayerController : Component
{
	[Property, FeatureEnabled( "Camera", Icon = "videocam" )] public bool UseCameraControls { get; set; } = true;

	[Property, Feature( "Camera" )] public float EyeDistanceFromTop { get; set; } = 8;
	[Property, Feature( "Camera" )] public bool ThirdPerson { get; set; } = true;
	[Property, Feature( "Camera" )] public bool HideBodyInFirstPerson { get; set; } = true;
	[Property, Feature( "Camera" )] public bool RotateWithGround { get; set; } = true;
	[Property, Feature( "Camera" )] public Vector3 CameraOffset { get; set; } = new Vector3( 256, 0, 12 );
	[Property, Feature( "Camera" ), InputAction] public string ToggleCameraModeButton { get; set; } = "view";

	float _cameraDistance = 100;
	float _eyez;

	void UpdateCameraPosition()
	{
		if ( !UseCameraControls ) return;
		if ( Scene.Camera is not CameraComponent cam ) return;

		if ( !string.IsNullOrWhiteSpace( ToggleCameraModeButton ) )
		{
			if ( Input.Pressed( ToggleCameraModeButton ) )
			{
				ThirdPerson = !ThirdPerson;
				_cameraDistance = 20;
			}
		}

		var rot = EyeAngles.ToRotation();
		cam.WorldRotation = rot;

		var eyePosition = WorldPosition + Vector3.Up * (BodyHeight - EyeDistanceFromTop);

		if ( IsOnGround && _eyez != 0 )
			eyePosition.z = _eyez.LerpTo( eyePosition.z, Time.Delta * 50 );

		_eyez = eyePosition.z;

		if ( !cam.RenderExcludeTags.Contains( "viewer" ) )
		{
			cam.RenderExcludeTags.Add( "viewer" );
		}

		if ( ThirdPerson )
		{
			var cameraDelta = rot.Forward * -CameraOffset.x + rot.Up * CameraOffset.z;

			// clip the camera
			var tr = Scene.Trace.FromTo( eyePosition, eyePosition + cameraDelta )
							.IgnoreGameObjectHierarchy( GameObject )
							.Radius( 8 )
							.Run();

			// smooth the zoom in and out
			if ( tr.StartedSolid )
			{
				_cameraDistance = _cameraDistance.LerpTo( cameraDelta.Length, Time.Delta * 100.0f );
			}
			else if ( tr.Distance < _cameraDistance )
			{
				_cameraDistance = _cameraDistance.LerpTo( tr.Distance, Time.Delta * 200.0f );
			}
			else
			{
				_cameraDistance = _cameraDistance.LerpTo( tr.Distance, Time.Delta * 2.0f );
			}


			eyePosition = eyePosition + cameraDelta.Normal * _cameraDistance;
		}

		GameObject.Tags.Set( "viewer", _cameraDistance < 20 || (!ThirdPerson && HideBodyInFirstPerson) );
		cam.WorldPosition = eyePosition;
		cam.FieldOfView = Preferences.FieldOfView;

		IEvents.PostToGameObject( GameObject, x => x.PostCameraSetup( cam ) );
	}
}
