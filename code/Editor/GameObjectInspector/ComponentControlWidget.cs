using Microsoft.VisualBasic;
using Sandbox;
using System.Linq;
using static Editor.Button;

namespace Editor;

[CustomEditor( typeof( BaseComponent ) )]
public class ComponentControlWidget : ControlWidget
{
	public ComponentControlWidget( SerializedProperty property ) : base( property )
	{
		SetSizeMode( SizeMode.Default, SizeMode.Default );

		Layout = Layout.Column();
		Layout.Spacing = 2;

		AcceptDrops = true;
	}

	protected override Vector2 SizeHint() => new Vector2( 10000, 22 );

	protected override void OnContextMenu( ContextMenuEvent e )
	{
		var m = new Menu( this );

	//	m.AddOption( "Copy", action: Copy );
	//	m.AddOption( "Paste", action: Paste );
		m.AddOption( "Clear", action: Clear );

		m.OpenAtCursor( true );
	}

	protected override void PaintControl()
	{
		var rect = LocalRect.Shrink( 6, 0 );
		var component = SerializedProperty.GetValue<BaseComponent>();
		var type = EditorTypeLibrary.GetType( SerializedProperty.PropertyType );

		if ( component is null )
		{
			Paint.SetPen( Theme.ControlText.WithAlpha( 0.3f ) );
			Paint.DrawIcon( rect, type?.Icon, 14, TextFlag.LeftCenter );
			rect.Left += 22;
			Paint.DrawText( rect, $"None ({type?.Name})", TextFlag.LeftCenter );
			Cursor = CursorShape.None;
		}
		else
		{
			Paint.SetPen( Theme.Green );
			Paint.DrawIcon( rect, type?.Icon, 14, TextFlag.LeftCenter );
			rect.Left += 22;
			Paint.DrawText( rect, $"{component.GetType()} (on {component.GameObject.Name})", TextFlag.LeftCenter );
			Cursor = CursorShape.Finger;
		}
	}

	protected override void OnMouseClick( MouseEvent e )
	{
		if ( e.LeftMouseButton )
		{
			e.Accepted = true;
			var go = SerializedProperty.GetValue<BaseComponent>();

			if ( go is not null )
			{
				SceneEditorSession.Active?.Selection.Set( go.GameObject );
				SceneEditorSession.Active.FullUndoSnapshot( $"Selected {go.GameObject}" );
			}
		}
	}

	void Clear()
	{
		SerializedProperty.SetValue<BaseComponent>( null );
	}

	public override void OnDragHover( DragEvent ev )
	{
		ev.Action = DropAction.Ignore;

		if ( ev.Data.Object is GameObject )
		{
			ev.Action = DropAction.Link;
			return;
		}
	}

	public override void OnDragDrop( DragEvent ev )
	{
		if ( ev.Data.Object is GameObject go )
		{
			var c = go.GetComponent( SerializedProperty.PropertyType, false, false );

			SerializedProperty.SetValue( c );
			return;
		}
	}
}
