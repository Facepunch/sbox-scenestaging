
using Sandbox.Helpers;
using System;
using System.Text.Json.Nodes;

public partial class SceneEditorSession
{
	public Vector3 CameraPosition = Vector3.One;
	public Rotation CameraRotation = Rotation.Identity;

	class SceneState
	{
		public Vector3 CameraPosition { get; set; }
		public Rotation CameraRotation { get; set; }
	}

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

	string CookieName
	{
		get
		{
			if ( Scene.Source is null ) return null;
			return Scene.Source.ResourcePath;
		}
	}

	public void InitializeCamera()
	{
		var cookieName = CookieName;

		// 1. load last camera position from cookies if possible
		if ( cookieName is not null && ProjectCookie.Get<SceneState>( $"{cookieName}.state", null ) is SceneState state )
		{
			CameraPosition = state.CameraPosition;
			CameraRotation = state.CameraRotation;
			return;
		}

		//
		// 2. Place camera where a Camera component is
		//
		var cc = Scene.GetComponent<CameraComponent>( true, true );
		if ( cc is not null )
		{
			CameraPosition = cc.Transform.Position;
			CameraRotation = cc.Transform.Rotation;
			return;
		}

		//
		// 3. BBox frame the scene
		//

		CameraRotation = Rotation.From( 45, 45, 0 );

		var fieldOfView = 80.0f;
		var bounds = Scene.GetBounds();
		var distance = MathX.SphereCameraDistance( bounds.Size.Length * 0.5f, fieldOfView ) * 1.0f;
		CameraPosition = bounds.Center + distance * CameraRotation.Backward;
	}

	void SaveState()
	{
		// Don't save the state of play sessions
		if ( this is GameEditorSession ) return;

		var cookieName = CookieName;

		var state = new SceneState
		{
			CameraPosition = CameraPosition,
			CameraRotation = CameraRotation
		};

		ProjectCookie.Set( $"{cookieName}.state", state );
	}

}
