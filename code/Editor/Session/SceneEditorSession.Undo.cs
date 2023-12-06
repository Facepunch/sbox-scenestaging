
using Sandbox.Helpers;
using System;
using System.Text.Json;
using System.Text.Json.Nodes;

public partial class SceneEditorSession
{
	UndoSystem undoSystem;
	Action pendingUndoSnapshot;

	private void InitUndo()
	{
		undoSystem = new UndoSystem();
		undoSystem.SetSnapshotFunction( snapshotForUndo );
		undoSystem.Initialize();

		// annoy everyone as much as possible
		undoSystem.OnUndo = ( x ) => EditorUtility.PlayRawSound( "sounds/editor/success.wav" );
		undoSystem.OnRedo = ( x ) => EditorUtility.PlayRawSound( "sounds/editor/success.wav" );
	}

	// the amount of frames no mouse button has been pressed for
	int mouseUpFrames;

	// whether we should defer undos or not
	bool ShouldDeferUndo => mouseUpFrames < 2;

	/// <summary>
	/// Take a full scene snapshot for the undo system. This is usually a last resort, if you can't do anything more incremental.
	/// </summary>
	public void FullUndoSnapshot( string title )
	{
		// if they have the mouse down (dragging into position etc)
		// then we wait until they're not before we take a snapshot
		if ( ShouldDeferUndo )
		{
			pendingUndoSnapshot = () => FullUndoSnapshot( title + " (deferred)" );
			return;
		}

		try
		{
			undoSystem.Snapshot( title );
		}
		finally
		{
			pendingUndoSnapshot = null;
		}
	}

	private void TickPendingUndoSnapshot()
	{
		mouseUpFrames++;

		if ( Editor.Application.MouseButtons != 0 )
		{
			mouseUpFrames = 0;
		}

		if ( pendingUndoSnapshot is null ) return;
		if ( ShouldDeferUndo ) return;

		pendingUndoSnapshot();
	}

	public bool Undo()
	{
		return undoSystem.Undo();
	}

	public bool Redo()
	{
		return undoSystem.Redo();
	}

	private Action snapshotForUndo( )
	{
		var o = new JsonSerializerOptions { MaxDepth = 512, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault };
		var jn = new JsonDocumentOptions { MaxDepth = 512 };
		var state = Scene.Serialize().ToJsonString( o );
		var selection = Selection.OfType<GameObject>().Select( x => x.Id ).ToArray();

		return () =>
		{
			Scene.Clear();

			using var sceneScope = Scene.Push();
			var js = JsonObject.Parse( state, documentOptions: jn ) as JsonObject;
			Scene.Deserialize( js );

			Selection.Clear();

			foreach( var o in selection )
			{
				if ( Scene.Directory.FindByGuid( o ) is GameObject go )
				{
					Selection.Add( go );
				}
			}
			
		};
	}
}
