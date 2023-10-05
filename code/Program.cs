using Sandbox;
using System.Linq;

public static class Program
{
	public static void Main()
	{
		//
		// In the future this won't exist, we'll have an option for "default scene"
		// and when you press play it'll load that.
		//

		Scene.Active = new Scene();
		Scene.Active.LoadFromFile( "turret.scene" );

		GameManager.IsPlaying = true;
	}
}
