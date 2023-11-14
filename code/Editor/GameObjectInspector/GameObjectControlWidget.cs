using Microsoft.VisualBasic;
using Sandbox;
using System;
using System.Linq;
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
		var rect = LocalRect.Shrink( 6, 0 );
		var go = SerializedProperty.GetValue<GameObject>();
		if ( !go.IsValid() )
		{
			Paint.SetPen( Theme.ControlText.WithAlpha( 0.3f ) );
			Paint.DrawIcon( rect, "radio_button_unchecked", 14, TextFlag.LeftCenter );
			rect.Left += 22;
			Paint.DrawText( rect, "None (GameObject)", TextFlag.LeftCenter );
			Cursor = CursorShape.None;
		}
		else if ( go is PrefabScene )
		{
			Paint.SetPen( Theme.Blue );
			Paint.DrawIcon( rect, "panorama_wide_angle_select", 14, TextFlag.LeftCenter );
			rect.Left += 22;
			Paint.DrawText( rect, go.Name, TextFlag.LeftCenter );
		}
		else
		{
			Paint.SetPen( Theme.Green );
			Paint.DrawIcon( rect, "panorama_wide_angle_select", 14, TextFlag.LeftCenter );
			rect.Left += 22;
			Paint.DrawText( rect, BuildName( go ), TextFlag.LeftCenter );
			Cursor = CursorShape.Finger;
		}
	}

	private string BuildName( GameObject go )
	{
		string str = "";

		if ( go.Parent is not null && go.Parent is not Scene )
		{
			str += $"{BuildName( go.Parent )} > ";
		}

		str += $"{go.Name}";

		return str;
	}

	protected override void OnMouseClick( MouseEvent e )
	{
		if ( e.LeftMouseButton )
		{
			e.Accepted = true;
			var go = SerializedProperty.GetValue<GameObject>();
			if ( go is not null && go is not PrefabScene )
			{
				SceneEditorSession.Active?.Selection.Set( go );
				SceneEditorSession.Active.FullUndoSnapshot( $"Selected {go}" );
			}
		}
	}

	void Clear()
	{
		SerializedProperty.SetValue<GameObject>( null );
	}

	public override void OnDragHover( DragEvent ev )
	{
		ev.Action = DropAction.Ignore;

		if ( ev.Data.Object is GameObject go )
		{
			ev.Action = DropAction.Link;
			return;
		}

		if ( ev.Data.HasFileOrFolder )
		{
			var asset = AssetSystem.FindByPath( ev.Data.Files.First() );
			if ( asset is null ) return;
			if ( asset.AssetType.FileExtension != PrefabFile.FileExtension ) return;

			if ( asset.TryLoadResource( out PrefabFile prefabFile ) )
			{
				ev.Action = DropAction.Link;
				return;
			}
		}

	}

	public override void OnDragDrop( DragEvent ev )
	{

		if ( ev.Data.Object is GameObject go )
		{
			SerializedProperty.SetValue( go );
			return;
		}

		if ( ev.Data.HasFileOrFolder )
		{
			var asset = AssetSystem.FindByPath( ev.Data.Files.First() );
			if ( asset is null ) return;
			if ( asset.AssetType.FileExtension != PrefabFile.FileExtension ) return;

			if ( asset.TryLoadResource( out PrefabFile prefabFile ) )
			{
				SerializedProperty.SetValue( prefabFile.Scene );
				return;
			}
		}
	}
}
