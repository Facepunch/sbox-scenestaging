using Editor;
namespace Sandbox;

public static partial class SceneExtensions
{
	/// <summary>
	/// Save a scene
	/// </summary>
	public static void Save( this Scene scene, bool saveAs )
	{
		var saveLocation = "";

		var a = scene.Save();

		if ( scene.Source is not null )
		{
			var asset = AssetSystem.FindByPath( scene.Source.ResourcePath );
			if ( asset is not null )
			{
				saveLocation = asset.AbsolutePath;
			}
		}
		else
		{
			saveAs = true;
		}

		string extension = "scene";
		string fileType = "Scene File";

		if ( scene is PrefabScene )
		{
			extension = "object";
			fileType = "Prefab File";
		}

		if ( saveAs || string.IsNullOrEmpty( saveLocation ) )
		{
			var lastDirectory = Cookie.GetString( $"LastSaveLocation.{extension}", "" );

			var fd = new FileDialog( null );
			fd.Title = $"Save {fileType}..";
			fd.Directory = lastDirectory;
			fd.DefaultSuffix = $".{scene}";
			fd.SelectFile( saveLocation );
			fd.SetFindFile();
			fd.SetModeSave();
			fd.SetNameFilter( $"{fileType} (*.{extension})" );

			if ( !fd.Execute() )
				return;

			saveLocation = fd.SelectedFile;
		}

		var sceneAsset = AssetSystem.CreateResource( extension, saveLocation );
		sceneAsset.SaveToDisk( a );
		scene.ClearUnsavedChanges();

	}

	/// <summary>
	/// We should make this globally reachanle at some point. Should be able to draw icons using bitmaps etc too.
	/// </summary>
	public static Editor.Menu CreateContextMenu( this Scene scene, Widget parent = null )
	{
		var menu = new Editor.Menu( parent );

		menu.AddOption( "Save", "save", action: () => scene.Save( false ) ).Enabled = scene.HasUnsavedChanges && scene.Source is not null;
		menu.AddOption( "Save Scene As..", action: () => scene.Save( true ) );

		return menu;

	}
}
