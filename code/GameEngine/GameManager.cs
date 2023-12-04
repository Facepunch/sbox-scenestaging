using Sandbox;
using System.Linq;

public static class GameManager
{
	public static bool IsPlaying { get; set; }
	public static bool IsPaused { get; set; }

	public static Scene ActiveScene { get; set; }


	[Event( "frame" )]
	public static void Frame()
	{
		if ( !GameManager.IsPlaying )
			return;

		if ( ActiveScene is null )
			return;

		if ( ActiveScene.IsLoading )
			return;

		LoadingScreen.IsVisible = false;

		using ( Sandbox.Utility.Superluminal.Scope( "Scene.GameTick", Color.Cyan ) )
		{
			ActiveScene.GameTick();
		}

		using ( Sandbox.Utility.Superluminal.Scope( "Scene.PreRender", Color.Cyan ) )
		{
			ActiveScene.PreRender();
		}

		var cameras = ActiveScene.GetAllComponents<CameraComponent>().OrderBy( x => x.Priority );
		foreach ( var cam in cameras )
		{
			cam.UpdateCamera();
		}
	}

	[Event( "camera.post" )]
	public static void PostCamera()
	{
		if ( !GameManager.IsPlaying )
		{
			Camera.Main.AddToRenderList();
			return;
		}

		if ( ActiveScene is null )
			return;

		var cameras = ActiveScene.GetAllComponents<CameraComponent>().OrderBy( x => x.Priority );
		foreach ( var cam in cameras )
		{
			cam.AddToRenderList();
		}
	}
}
