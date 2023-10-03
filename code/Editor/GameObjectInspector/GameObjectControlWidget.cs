using Sandbox;
using static Editor.Button;

namespace Editor;

[CustomEditor( typeof( GameObject ) )]
public class GameObjectControlWidget : ControlWidget
{
	public GameObjectControlWidget( SerializedProperty property ) : base( property )
	{
		SetSizeMode( SizeMode.Default, SizeMode.Default );

		Layout = Layout.Column();
		Layout.Spacing = 2;

		AcceptDrops = true;
	}

	protected override Vector2 SizeHint() => new Vector2( 10000, 22 );

	protected override void OnContextMenu( ContextMenuEvent e )
	{
		var m = new Menu();

	//	m.AddOption( "Copy", action: Copy );
	//	m.AddOption( "Paste", action: Paste );
		m.AddOption( "Clear", action: Clear );

		m.OpenAtCursor();
	}

	protected override void PaintControl()
	{
		var go = SerializedProperty.GetValue<GameObject>();
		if ( go is null )
		{
			Paint.SetPen( Theme.ControlText.WithAlpha( 0.3f ) );
			Paint.DrawText( LocalRect.Shrink( 8, 2 ), "None (GameObject)", TextFlag.LeftCenter );
		}
		else
		{
			Paint.SetPen( Theme.ControlText );
			Paint.DrawText( LocalRect.Shrink( 8, 2 ), SerializedProperty.As.String, TextFlag.LeftCenter );
		}
	}

	void Clear()
	{
		SerializedProperty.SetValue<GameObject>( null );
	}

	public override void OnDragHover( DragEvent ev )
	{
		if ( ev.Data.Object is GameObject go )
		{
			ev.Action = DropAction.Link;
			return;
		}

		ev.Action = DropAction.Ignore;
	}

	public override void OnDragDrop( DragEvent ev )
	{
		if ( ev.Data.Object is GameObject go )
		{
			SerializedProperty.SetValue( go );
			return;
		}
	}
}
