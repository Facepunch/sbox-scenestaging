
using Editor;
using System.Linq;
using System.Text.Json.Nodes;

public static class SceneEditorMenus
{
	[Menu( "Editor", "Scene/Cut", Shortcut = "Ctrl+X" )]
	public static void Cut()
	{
		var go = EditorScene.Selection.First() as GameObject;

		var json = go.Serialize();
		EditorUtility.Clipboard.Copy( json.ToString() );
	}


	[Menu( "Editor", "Scene/Copy", Shortcut = "Ctrl+C" )]
	public static void Copy()
	{
		var go = EditorScene.Selection.First() as GameObject;

		var json = go.Serialize();
		EditorUtility.Clipboard.Copy( json.ToString() );
	}

	[Menu( "Editor", "Scene/Paste", Shortcut = "Ctrl+V" )]
	public static void Paste()
	{
		var selected = EditorScene.Selection.First() as GameObject;

		var text = EditorUtility.Clipboard.Paste();
		if ( JsonNode.Parse( text ) is JsonObject jso )
		{
			var go = EditorScene.Active.CreateObject();
			go.Deserialize( jso );

			if ( selected is not null )
			{
				selected.AddSibling( go, false );
			}

			go.MakeNameUnique();

			EditorScene.Selection.Clear();
			EditorScene.Selection.Add( go );

			//TreeView.SelectItem( go );
		}
	}

	[Menu( "Editor", "Scene/Paste As Child", Shortcut = "Ctrl+Shift+V" )]
	public static void PasteAsChild()
	{
		var selected = EditorScene.Selection.First() as GameObject;

		var text = EditorUtility.Clipboard.Paste();
		if ( JsonNode.Parse( text ) is JsonObject jso )
		{
			var go = EditorScene.Active.CreateObject();
			go.Deserialize( jso );
			go.SetParent( selected );
			go.MakeNameUnique();

			// treeview - open parent selected somehow

			EditorScene.Selection.Clear();
			EditorScene.Selection.Add( go );
		}
	}

	[Menu( "Editor", "Scene/Duplicate", Shortcut = "Ctrl+D" )]
	public static void Duplicate()
	{
		var source = EditorScene.Selection.First() as GameObject;
		var json = source.Serialize();

		var go = EditorScene.Active.CreateObject();
		go.Deserialize( json );

		source.AddSibling( go, false );
		go.MakeNameUnique();

		EditorScene.Selection.Clear();
		EditorScene.Selection.Add( go );
	}

	[Menu( "Editor", "Scene/Delete", Shortcut = "Del" )]
	public static void Delete()
	{
		foreach ( var entry in EditorScene.Selection.OfType<GameObject>() )
		{
			entry.Destroy();
		}
	}
}
