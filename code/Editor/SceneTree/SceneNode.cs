
using Editor;
using System;
using System.Linq;

public partial class SceneNode : TreeNode<Scene>
{
	public SceneNode( Scene scene ) : base ( scene )
	{

	}

	public override bool HasChildren => Value.All.Any();

	protected override void BuildChildren()
	{
		var children = Children.ToList();

		foreach ( var child in Value.All )
		{
			var c = children.OfType<GameObjectNode>().FirstOrDefault( x => x.Value == child );
			if ( c == null )
			{
				AddItem( new GameObjectNode( child ) );
			}
			else
			{
				children.Remove( c );
			}  
		}

		foreach ( var child in children )
		{
			RemoveItem( child ); 
		} 
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

	public override int ValueHash => HashCode.Combine( Value?.All.Count );


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

		GameObjectNode.CreateObjectMenu( m, go =>
		{
			Value.Register( go );
			TreeView.Open( this );
			TreeView.SelectItem( go );
		} );

		m.OpenAtCursor();

		return true;
	}


}

