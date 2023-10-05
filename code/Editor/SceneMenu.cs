
using Editor;

public static class SceneExtensions
{
	/// <summary>
	/// We should make this globally reachanle at some point. Should be able to draw icons using bitmaps etc too.
	/// </summary>
	public static Menu CreateContextMenu( this Scene scene )
	{
		var menu = new Menu();

		menu.AddOption( "Save", "save", action: () => scene.Save( false ) ).Enabled = scene.HasUnsavedChanges && scene.SourceSceneFile is not null;
		menu.AddOption( "Save Scene As..", action: () => scene.Save( true ) );

		return menu;

	}

	/// <summary>
	/// Save a scene
	/// </summary>
	public static void Save( this Scene scene, bool saveAs )
	{
		var saveLocation = "";

		var a = scene.Save();

		if ( scene.SourceSceneFile is not null )
		{
			var asset = AssetSystem.FindByPath( scene.SourceSceneFile.ResourcePath );
			if ( asset is not null )
			{
				saveLocation = asset.AbsolutePath;
			}
		}

		if ( saveAs )
		{
			var lastDirectory = Cookie.GetString( "LastSaveSceneLocation", "" );

			var fd = new FileDialog( null );
			fd.Title = $"Save Scene As..";
			fd.Directory = lastDirectory;
			fd.DefaultSuffix = $".scene";
			fd.SelectFile( saveLocation );
			fd.SetFindFile();
			fd.SetModeSave();
			fd.SetNameFilter( $"Scene File (*.scene)" );

			if ( !fd.Execute() )
				return;

			saveLocation = fd.SelectedFile;
		}

		var sceneAsset = AssetSystem.CreateResource( "scene", saveLocation );
		sceneAsset.SaveToDisk( a );
		scene.ClearUnsavedChanges();

	}
}
