/// <summary>
/// Holds a current open scene and its edit state
/// </summary>
public class PrefabEditorSession : SceneEditorSession
{
	public new PrefabScene Scene => base.Scene as PrefabScene;

	public PrefabEditorSession( PrefabScene scene ) : base( scene )
	{
		scene.SceneWorld.AmbientLightColor = new Color( 0.7f, 0.7f, 0.7f );

		scene.Name = scene.Source.ResourceName;
		Selection.Add( scene );
	}

	public override void OnEdited()
	{
		if ( Scene is PrefabScene prefabScene )
		{
			EditorScene.UpdatePrefabInstances( prefabScene, prefabScene.Source as PrefabFile );
		}
	}
}
