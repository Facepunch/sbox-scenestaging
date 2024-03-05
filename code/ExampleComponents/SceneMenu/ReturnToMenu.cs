using Sandbox;

public sealed class ReturnToMenu : Component
{
	protected override void OnUpdate()
	{
		if ( Input.EscapePressed )
		{
			Game.ActiveScene.LoadFromFile( "scenes/tests/menu.scene" );
		}
	}
}
