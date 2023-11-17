global using System.Linq;
global using System;
global using System.Collections.Generic;
global using System.Threading.Tasks;
using Sandbox;


public static class Program
{
	public static void Main()
	{
		//
		// In the future this won't exist, we'll have an option for "default scene"
		// and when you press play it'll load that.
		//

		GameManager.ActiveScene = new Scene();
		GameManager.ActiveScene.LoadFromFile( "scenes/tests/menu.scene" );

		GameManager.IsPlaying = true;
	}
}
