using System;
/// <summary>
/// Holds a current open scene and its edit state
/// </summary>
public partial class SceneEditorSession
{
	public static List<SceneEditorSession> All { get; } = new ();
	public static SceneEditorSession Active { get; set; }

	// scenes that have been edited, but waiting for a set interval to react
	// just to debounce changes.
	private static HashSet<SceneEditorSession> editedScenes = new();

	public Scene Scene { get; set; }

	public SceneEditorSession( Scene scene )
	{
		Scene = scene;
		All.Add( this );

		Scene.OnEdited += OnSceneEdited;
		InitUndo();
	}

	public void Destroy()
	{
		Scene.OnEdited = null;

		// If this is the active scene
		// switch away to a sibling
		if ( this == Active )
		{
			Active = null;

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

	public void OnSceneEdited( string title )
	{
		editedScenes.Add( this );
		FullUndoSnapshot( title );
	}

	public void MakeActive()
	{
		Active = this;

		EditorWindow.DockManager.RaiseDock( "Scene" );
		UpdateEditorTitle();
	}

	RealTimeSince timeSinceSavedState;

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

		// Undo system might have a deferred snapshot
		TickPendingUndoSnapshot();

		// Save camera state to disk
		if ( timeSinceSavedState > 1.0f )
		{
			timeSinceSavedState = 0;
			SaveState();
		}
	}

	static void UpdateEditorTitle()
	{
		if ( Active is null )
			return;

		
		var name = Active.Scene.Name;
		if ( Active.Scene.HasUnsavedChanges ) name += "*";

		EditorWindow.UpdateEditorTitle( name );		
	}

	public virtual void OnEdited()
	{
		EditorEvent.Run( "scene.edited", Scene );
	}

	static RealTimeSince timeSinceLastUpdatePrefabs;

	public static void Tick()
	{
		ProcessSceneEdits();

		Active?.TickActive();
	}

	static void ProcessSceneEdits()
	{
		if ( timeSinceLastUpdatePrefabs < 0.1 ) return;
		timeSinceLastUpdatePrefabs = 0;

		foreach ( var session in editedScenes )
		{
			session.OnEdited();
		}

		editedScenes.Clear();
	}

	/// <summary>
	/// Pushes the active editor scene to the current scope
	/// </summary>
	public static IDisposable Scope()
	{
		return Active.Scene.Push();
	}

}
