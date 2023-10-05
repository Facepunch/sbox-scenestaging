using Sandbox;
using System.Linq;

public static class GameManager
{
	public static bool IsPlaying { get; set; }
	public static bool IsPaused { get; set; }


	[Event( "frame" )]
	public static void Frame()
	{
		if ( !GameManager.IsPlaying )
			return;

		Scene.Active.Tick();
		Scene.Active.PreRender();

		var camera = Scene.Active.FindAllComponents<CameraComponent>( true ).FirstOrDefault();

		if ( camera is not null )
		{
			camera.UpdateCamera( Camera.Main );
		}

	}
}
