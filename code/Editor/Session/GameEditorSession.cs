
/// <summary>
/// Loaded a scene in the editor, fly around and edit like any other scene.. 
/// except the game is also being played in the background.
/// </summary>
public class GameEditorSession : SceneEditorSession
{
	public GameEditorSession( Scene scene ) : base( scene )
	{

	}

	/// <summary>
	/// Close any active game editor sessions
	/// </summary>
	public static void CloseAll()
	{
		foreach( var a in All.OfType<GameEditorSession>().ToArray() )
		{
			a.Destroy();
		}
	}
}
