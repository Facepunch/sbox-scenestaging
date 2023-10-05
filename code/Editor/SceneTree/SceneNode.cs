using System;

public partial class SceneNode : TreeNode<Scene>
{
	public SceneNode( Scene scene ) : base( scene )
	{

	}

	public override bool HasChildren => Value.All.Any();

	protected override void BuildChildren()
	{
		SetChildren( Value.All, x => new GameObjectNode( x ) );
	}

	public override void OnPaint( VirtualWidget item )
	{
		var fullSpanRect = item.Rect;
		fullSpanRect.Left = 0;
		fullSpanRect.Right = TreeView.Width;

		if ( item.Selected )
		{
			Paint.ClearPen();
			Paint.SetBrush( Theme.Blue.WithAlpha( 0.4f ) );
			Paint.DrawRect( fullSpanRect );

			Paint.SetPen( Color.White );
		}
		else
		{
			Paint.SetPen( Theme.ControlText );
		}

		var r = item.Rect;
		Paint.SetPen( Theme.ControlText );

		r.Left += 4;
		Paint.DrawIcon( r, "perm_media", 14, TextFlag.LeftCenter );
		r.Left += 22;
		Paint.SetDefaultFont( 9 );
		Paint.DrawText( r, $"{Value.Name}", TextFlag.LeftCenter );
	}

	public override int ValueHash
	{
		get
		{
			HashCode hc = new HashCode();

			foreach ( var val in Value.All )
			{
				hc.Add( val );
			}

			return hc.ToHashCode();
		}
	}


	public override void OnKeyPress( KeyEvent e )
	{
		base.OnKeyPress( e );

		if ( e.Key == KeyCode.Delete || e.Key == KeyCode.Backspace )
		{

		}
	}

	public override bool OnContextMenu()
	{
		var m = new Menu();

		m.AddOption( "Save", action: () => SaveScene( Value, false ) ).Enabled = Value.SourceSceneFile is not null;
		m.AddOption( "Save Scene As..", action: () => SaveScene( Value, true ) );

		m.AddSeparator();

		GameObjectNode.CreateObjectMenu( m, go =>
		{
			Value.Register( go );
			TreeView.Open( this );
			TreeView.SelectItem( go );
		} );

		m.OpenAtCursor();

		return true;
	}

	public static void SaveScene( Scene scene, bool saveAs )
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

	}


}

