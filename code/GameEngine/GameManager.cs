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

		using ( Sandbox.Utility.Superluminal.Scope( "Scene.GameTick", Color.Cyan ) )
		{
			ActiveScene.GameTick();
		}

		using ( Sandbox.Utility.Superluminal.Scope( "Scene.PreRender", Color.Cyan ) )
		{
			ActiveScene.PreRender();
		}

		var camera = ActiveScene.FindAllComponents<CameraComponent>( true ).FirstOrDefault();

		if ( camera is not null )
		{
			camera.UpdateCamera( Camera.Main );
		}

	}
}
