
using System;

public partial class OpenSceneList : Widget
{
	public OpenSceneList( Widget parent ) : base( parent )
	{
		MinimumHeight = Theme.RowHeight;
		Layout = Layout.Row();
	}

	public void BuildUI()
	{
		Layout.Clear( true );

		if ( GameManager.IsPlaying )
		{
			AddSceneButton( Scene.Active );
		}

		foreach ( var scene in EditorScene.OpenScenes )
		{
			AddSceneButton( scene );
		}

		Layout.AddStretchCell();
	}

	void AddSceneButton( Scene scene )
	{
		var b = Layout.Add( new Button( scene.Name ) );

		b.Clicked = () =>
		{
			EditorScene.Active = scene;
		};

		if ( EditorScene.Active == scene )
		{
			b.SetStyles( "background-color: red;" );
		}
	}

	int rebuildHash;

	[EditorEvent.Frame]
	public void CheckForChanges()
	{
		HashCode hash = new();

		foreach ( var scene in EditorScene.OpenScenes )
		{
			hash.Add( scene );
		}

		hash.Add( EditorScene.Active );
		hash.Add( Scene.Active );

		if ( rebuildHash == hash.ToHashCode() ) return;
		rebuildHash = hash.ToHashCode();

		BuildUI();
	}
}

