
using Editor;
using Sandbox;
using System.Linq;
using System.Text.Json.Nodes;

public static class SceneEditorMenus
{
	[Menu( "Editor", "Scene/Save", Shortcut = "Ctrl+S" )]
	public static void SaveScene()
	{
		SceneEditorSession.Active.Scene.Save( false );
		EditorEvent.Run( "scene.saved", SceneEditorSession.Active.Scene );
	}

	[Menu( "Editor", "Scene/Save All", Shortcut = "Ctrl+Shift+S" )]
	public static void SaveAllScene()
	{
		foreach ( var session in SceneEditorSession.All )
		{
			if ( session is GameEditorSession ) continue;

			session.Scene.Save( false );
			EditorEvent.Run( "scene.saved", session.Scene );
		}
	}

	[Menu( "Editor", "Scene/Cut", Shortcut = "Ctrl+X" )]
	public static void Cut()
	{
		var options = new GameObject.SerializeOptions();
		var go = EditorScene.Selection.FirstOrDefault() as GameObject;
		if ( go is null ) return;

		var json = go.Serialize( options );

		EditorUtility.Clipboard.Copy( json.ToString() );
		go.Destroy();
	}


	[Menu( "Editor", "Scene/Copy", Shortcut = "Ctrl+C" )]
	public static void Copy()
	{
		var options = new GameObject.SerializeOptions();
		var go = EditorScene.Selection.FirstOrDefault() as GameObject;
		if ( go is null ) return;

		var json = go.Serialize( options );

		EditorUtility.Clipboard.Copy( json.ToString() );
	}

	[Menu( "Editor", "Scene/Paste", Shortcut = "Ctrl+V" )]
	public static void Paste()
	{
		var selected = EditorScene.Selection.First() as GameObject;

		if  ( selected  is Scene )
		{
			PasteAsChild();
			return;
		}

		using var scope = SceneEditorSession.Scope();
		using var initScope = SceneUtility.DeferInitializationScope( "paste" );

		var text = EditorUtility.Clipboard.Paste();
		if ( JsonNode.Parse( text ) is JsonObject jso )
		{
			SceneUtility.MakeGameObjectsUnique( jso );

			var go = SceneEditorSession.Active.Scene.CreateObject();
			go.Deserialize( jso );

			if ( selected is not null )
			{
				selected.AddSibling( go, false );
			}

			go.MakeNameUnique();

			EditorScene.Selection.Clear();
			EditorScene.Selection.Add( go );

			go.EditLog( "Paste", go );
			//TreeView.SelectItem( go );
		}
	}

	[Menu( "Editor", "Scene/Paste As Child", Shortcut = "Ctrl+Shift+V" )]
	public static void PasteAsChild()
	{
		var selected = EditorScene.Selection.First() as GameObject;

		using var scope = SceneEditorSession.Scope();
		using var initScope = SceneUtility.DeferInitializationScope( "paste" );

		var text = EditorUtility.Clipboard.Paste();
		if ( JsonNode.Parse( text ) is JsonObject jso )
		{
			SceneUtility.MakeGameObjectsUnique( jso );

			var go = SceneEditorSession.Active.Scene.CreateObject();
			go.Deserialize( jso );
			go.SetParent( selected );
			go.MakeNameUnique();

			// treeview - open parent selected somehow

			EditorScene.Selection.Clear();
			EditorScene.Selection.Add( go );

			go.EditLog( "Paste", go );
		}
	}

	[Menu( "Editor", "Scene/Duplicate", Shortcut = "Ctrl+D" )]
	public static void Duplicate()
	{
		using var scope = SceneEditorSession.Scope();
		using var initScope = SceneUtility.DeferInitializationScope( "duplicate" );

		var options = new GameObject.SerializeOptions();
		var source = EditorScene.Selection.First() as GameObject;

		if ( source is Scene ) return;

		var json = source.Serialize( options );
		SceneUtility.MakeGameObjectsUnique( json );

		var go = new GameObject();
		go.Deserialize( json );
		go.Transform.World = source.Transform.World;
		go.MakeNameUnique();

		source.AddSibling( go, false );

		go.EditLog( "Duplicate", go );

		EditorScene.Selection.Clear();
		EditorScene.Selection.Add( go );
	}

	[Menu( "Editor", "Scene/Delete", Shortcut = "Del" )]
	public static void Delete()
	{
		using var scope = SceneEditorSession.Scope();
		int deleted = 0;

		foreach ( var entry in EditorScene.Selection.OfType<GameObject>().ToArray() )
		{
			if ( !entry.IsDeletable() )
				return;

			var nextSelect = entry.GetNextSibling( false );
			if ( nextSelect is null ) nextSelect = entry.Parent;

			if ( SceneEditorSession.Active.Selection.Contains( entry ) )
			{
				SceneEditorSession.Active.Selection.Clear();
				SceneEditorSession.Active.Selection.Add( nextSelect );
			}

			entry.Destroy();
			deleted++;
		}

		if ( deleted > 0 )
		{
			SceneEditorSession.Active.Scene.EditLog( $"Deleted {deleted} Objects", SceneEditorSession.Active.Scene );
		}
	}

	[Menu( "Editor", "Scene/Frame", Shortcut = "f" )]
	public static void Frame()
	{
		var bbox = new BBox();

		int i = 0;
		foreach ( var entry in EditorScene.Selection.OfType<GameObject>() )
		{
			if ( i++ == 0 )
			{
				bbox = new BBox( entry.Transform.Position, 16 );
			}

			// get the bounding box of the selected objects
			bbox = bbox.AddBBox( new BBox( entry.Transform.Position, 16 ) );

			foreach ( var model in entry.GetComponents<ModelComponent>( true, true ) )
			{
				bbox = bbox.AddBBox( model.Bounds );
			}
		}

		EditorEvent.Run( "scene.frame", bbox );
	}


	[Menu( "Editor", "Scene/Align To View" )]
	public static void AlignToView()
	{
		if ( !SceneViewWidget.Current.IsValid() )
			return;

		if ( EditorScene.Selection.Count == 0 )
			return;

		foreach ( var entry in EditorScene.Selection.OfType<GameObject>() )
		{
			entry.Transform.World = new Transform( SceneViewWidget.Current.Camera.Position, SceneViewWidget.Current.Camera.Rotation );

		}

		SceneEditorSession.Active.Scene.EditLog( "Align To View", EditorScene.Selection.ToArray() );
	}


	[Menu( "Editor", "Edit/Undo", Shortcut = "Ctrl+z" )]
	public static void Undo()
	{
		using var scope = SceneEditorSession.Scope();
		SceneEditorSession.Active.Undo();
	}

	[Menu( "Editor", "Edit/Redo", Shortcut = "Ctrl+y" )]
	public static void Redo()
	{
		using var scope = SceneEditorSession.Scope();
		SceneEditorSession.Active.Redo();
	}
}
