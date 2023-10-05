using Sandbox;
using System.Linq;

public static class Program
{
	public static void Main()
	{
		//	if ( Application.IsEditor )
		//		return;

		Log.Info( "Loading Scene.." );
		Scene.Active = new Scene();
		Scene.Active.LoadFromFile( "turret.scene" );

		Log.Info( "Playing.." );
		GameManager.IsPlaying = true;
	}


}
