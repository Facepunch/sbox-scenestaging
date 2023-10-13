
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

}
