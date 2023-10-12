/// <summary>
/// Holds a current open scene and its edit state
/// </summary>
public class SceneEditorSession
{
	public static List<SceneEditorSession> All { get; } = new ();
	public static SceneEditorSession Active { get; set; }

	public Scene Scene { get; set; }

	public SceneEditorSession( Scene scene )
	{
		Scene = scene;
		All.Add( this );

		Scene.OnEdited += () => EditorScene.OnSceneEdited( this );
	}

	public void Destroy()
	{
		// If this is the active scene
		// switch away to a sibling
		if ( this == Active )
		{
			var index = All.IndexOf( this );
			if ( index >= 0 && All.Count > 1 )
			{
				if ( index > 0 ) index--;
				else index++;

				Active = All[index];
			}
		}

		All.Remove( this );
	}

	public void MakeActive()
	{
		Active = this;

		EditorWindow.DockManager.RaiseDock( "Scene" );
		UpdateEditorTitle();
	}

	public void TickActive()
	{
		//
		// Game isn't playing - control the main camera
		//
		if ( !GameManager.IsPlaying )
		{
			var camera = Scene.FindAllComponents<CameraComponent>( false ).FirstOrDefault();

			if ( camera is not null )
			{
				camera.UpdateCamera( Camera.Main );
			}
		}

		//
		// If this is an editor scene, tick it to flush deleted objects etc
		//
		Scene.ProcessDeletes();
	}

	static void UpdateEditorTitle()
	{
		if ( Active is null )
			return;

		
		var name = Active.Scene.Name;
		if ( Active.Scene.HasUnsavedChanges ) name += "*";

		EditorWindow.UpdateEditorTitle( name );		
	}
}
