
using Sandbox.Helpers;
using System;
using System.Text.Json.Nodes;

public partial class SceneEditorSession
{
	public Vector3 CameraPosition = Vector3.One;
	public Rotation CameraRotation = Rotation.Identity;

	public struct SceneState
	{
		public Vector3 CameraPosition { get; set; }
		public Rotation CameraRotation { get; set; }
	}

	SceneState LastState { get; set; }


	public void UpdateState( SceneCamera camera )
	{
		CameraPosition = camera.Position;
		CameraRotation = camera.Rotation;
	}

	public void RestoreCamera( SceneCamera camera )
	{
		camera.Position = CameraPosition;
		camera.Rotation = CameraRotation;
	}

	public void InitializeCamera()
	{
		// todo - load last camera position from cookies if possible


		CameraRotation = Rotation.From( 45, 45, 0 );

		var fieldOfView = 80.0f;
		var bounds = Scene.GetBounds();
		var distance = MathX.SphereCameraDistance( bounds.Size.Length * 0.5f, fieldOfView ) * 1.0f;
		CameraPosition = bounds.Center + distance * CameraRotation.Backward;
	}

}
