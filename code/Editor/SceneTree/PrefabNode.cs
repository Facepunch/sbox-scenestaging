using System;

public partial class PrefabNode : GameObjectNode
{
	public PrefabNode( PrefabScene go ) : base( go )
	{

	}

	public override bool HasChildren => Value.Children.Any();

	protected override void BuildChildren() => SetChildren( Value.Children, x => new GameObjectNode( x ) );
	protected override bool HasDescendant( object obj ) => obj is GameObject go && Value.IsDescendant( go );

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


		Paint.SetPen( Theme.Blue );
		Paint.DrawIcon( r, "circle", 14, TextFlag.LeftCenter );
		r.Left += 22;

		Paint.SetDefaultFont();
		Paint.SetPen( Theme.ControlText );
		Paint.DrawText( r, $"{Value.Name}", TextFlag.LeftCenter );
	}

	public override int ValueHash
	{
		get
		{
			HashCode hc = new HashCode();

			foreach ( var val in Value.Children )
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

		m.AddOption( "Save Prefab", action: () => Save( Value, false ) );
		m.AddOption( "Save As..", action: () => Save( Value, true ) );

		m.AddSeparator();

		AddGameObjectMenuItems( m );

		m.OpenAtCursor();

		return true;
	}

	public static void Save( GameObject obj, bool saveAs )
	{
		var scene = obj.Scene;
		var saveLocation = "";

		var a = obj.GetAsPrefab();

		if ( scene is PrefabScene prefabScene )
		{
			var asset = AssetSystem.FindByPath( prefabScene.Source.ResourcePath );
			if ( asset is not null )
			{
				saveLocation = asset.AbsolutePath;
			}
		}

		if ( saveAs )
		{
			var lastDirectory = Cookie.GetString( "LastSaveSceneLocation", "" );

			var fd = new FileDialog( null );
			fd.Title = $"Save Prefab As..";
			fd.Directory = lastDirectory;
			fd.DefaultSuffix = $".object";
			fd.SelectFile( saveLocation );
			fd.SetFindFile();
			fd.SetModeSave();
			fd.SetNameFilter( $"Prefab (*.object)" );

			if ( !fd.Execute() )
				return;

			saveLocation = fd.SelectedFile;
		}

		var sceneAsset = AssetSystem.CreateResource( "object", saveLocation );
		sceneAsset.SaveToDisk( a );

	}


}

