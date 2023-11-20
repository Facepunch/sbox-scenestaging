using System;

public partial class SceneNode : TreeNode<Scene>
{
	public SceneNode( Scene scene ) : base( scene )
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

		var r = item.Rect;
		Paint.SetPen( Theme.ControlText.WithAlpha( 0.4f ) );

		r.Left += 4;
		Paint.DrawIcon( r, "perm_media", 14, TextFlag.LeftCenter );
		r.Left += 22;
		Paint.SetDefaultFont();
		Paint.DrawText( r, $"{Value.Name}", TextFlag.LeftCenter );
	}

	public override int ValueHash
	{
		get
		{
			if ( Value?.Children is null )
				return 0;

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
		var m = new Menu( TreeView );



		m.AddSeparator();

		GameObjectNode.CreateObjectMenu( m, go =>
		{
			go.Parent = Value;
			TreeView.Open( this );
			TreeView.SelectItem( go );
		} );

		m.OpenAtCursor( true );

		return true;
	}



}

