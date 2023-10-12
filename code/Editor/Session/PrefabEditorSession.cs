/// <summary>
/// Holds a current open scene and its edit state
/// </summary>
public class PrefabEditorSession : SceneEditorSession
{
	public new PrefabScene Scene => base.Scene as PrefabScene;

	public PrefabEditorSession( PrefabScene scene ) : base( scene )
	{

	}

	public override void OnEdited()
	{
		if ( Scene is PrefabScene prefabScene )
		{
			EditorScene.UpdatePrefabInstances( prefabScene.Source as PrefabFile );
		}
	}
}
