using Sandbox;

public sealed class ReturnToMenu : BaseComponent
{
	protected override void OnUpdate()
	{
		if ( Input.EscapePressed )
		{
			GameManager.ActiveScene.LoadFromFile( "scenes/tests/menu.scene" );
		}
	}
}
