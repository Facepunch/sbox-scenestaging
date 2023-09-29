using Editor.Inspectors;
using Sandbox;
using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using Tools;
namespace Editor.EntityPrefabEditor;

public partial class ComponentSheet : Widget
{
	SerializedObject TargetObject;
	Layout Content;

	public ComponentSheet( SerializedObject target, Action contextMenu ) : base( null )
	{
		Name = "ComponentSheet";
		TargetObject = target;
		Layout = Layout.Column();
		SetSizeMode( SizeMode.Default, SizeMode.CanShrink );

		var header = Layout.Add( new ComponentHeader( TargetObject, this ) );
		header.MouseRightPress += contextMenu;

		Content = Layout.AddColumn();
		Frame();
	}

	int lastHash;

	[EditorEvent.Frame]
	public void Frame()
	{
		var hash = HashCode.Combine( TargetObject );

		if ( lastHash != hash )
		{
			lastHash = hash;
			RebuildContent();
		}
	}

	void RebuildContent()
	{
		Content.Clear( true );

		BuildInstanceContent();
	}

	void BuildInstanceContent()
	{
		var props = TargetObject.Where( x => x.HasAttribute<PropertyAttribute>() );

		var ps = new ControlSheet();

		foreach( var prop in props.OrderBy( x => x.SourceLine ).ThenBy( x => x.DisplayName ) )
		{
			ps.AddRow( prop );
		}
		
		Content.Add( ps );
	}

	public override void ChildValuesChanged( Widget source )
	{
		//TargetObject.IsChanged();
	}

}

file class ComponentHeader : Widget
{
	SerializedObject TargetObject { get; init; }

	Layout expanderRect;
	Layout iconRect;
	Layout textRect;

	public ComponentHeader( SerializedObject target, Widget parent ) : base( parent )
	{
		TargetObject = target;

		var enabled = ControlWidget.Create( TargetObject.GetProperty( "Enabled" ) );
		enabled.FixedWidth = 18;
		enabled.FixedHeight = 18;

		FixedHeight = 22;

		ContentMargins = 0;
		Layout = Layout.Row();

		expanderRect = Layout.AddRow();
		expanderRect.AddSpacingCell( 22 );

		iconRect = Layout.AddRow();
		iconRect.AddSpacingCell( 22 );

		Layout.AddSpacingCell( 4 );

		// icon

		Layout.Add( enabled );

		Layout.AddSpacingCell( 8 );

		// text 
		textRect = Layout.AddColumn( 1 );
	}

	protected override void OnPaint()
	{
		base.OnPaint();

		float opacity = 0.6f;
		Paint.Antialiasing = true;
		Paint.TextAntialiasing = true;

		bool expanded = true;


		if ( Paint.HasMouseOver )
		{
			//Paint.ClearPen();
			//Paint.SetBrushRadial( 30, 256, Theme.Blue.WithAlpha( 0.1f ), Theme.Blue.WithAlpha( 0.0f ) ) ;
			//Paint.DrawRect( LocalRect, 0 );
			opacity = 1.0f;
		}

		if ( !expanded )
		{
			Paint.ClearPen();
			Paint.SetBrushRadial( new Vector2( 64, 0 ), 512, Color.Black.WithAlpha( 0.1f ), Color.Black.WithAlpha( 0.0f ) );
			Paint.DrawRect( LocalRect, 0 );
		}
		else
		{
			Paint.ClearPen();
			Paint.SetBrushRadial( new Vector2( 64, 0 ), 256, Theme.Blue.WithAlpha( 0.08f ), Theme.Blue.WithAlpha( 0.03f ) );
			Paint.DrawRect( LocalRect, 0 );

			Paint.SetPen( Theme.Black.WithAlpha( 0.4f ), 2 );
			Paint.DrawLine( LocalRect.BottomLeft, LocalRect.BottomRight );
		}

		var row = LocalRect;

		Paint.SetPen( Theme.White.WithAlpha( opacity * 0.5f ) );
		Paint.DrawIcon( expanderRect.InnerRect, expanded ? "keyboard_arrow_down" : "chevron_right", 16, TextFlag.Center );

		Paint.SetPen( Theme.Blue.WithAlpha( opacity ) );
		Paint.DrawIcon( iconRect.InnerRect, TargetObject.TypeIcon ?? "category", 14, TextFlag.Center ); 

		Paint.SetPen( Theme.Blue.Lighten( 0.1f ).WithAlpha( (expanded ? 0.9f : 0.6f) * opacity ) );
		Paint.SetDefaultFont( 8, 1000, false );
		Paint.DrawText( textRect.InnerRect, TargetObject.TypeTitle, TextFlag.LeftCenter );
	}

	protected override void OnContextMenu( ContextMenuEvent e )
	{
		base.OnContextMenu( e );

		var menu = new Menu();
		//menu.AddOption( "Delete Component", "clear", () => Target.Parent.Components.Remove( Target ) );
		menu.OpenAtCursor( false );
	}

	protected override void OnMouseClick( MouseEvent e )
	{
		base.OnMouseClick( e );

		if ( e.LeftMouseButton )
		{
		//	Target.ToggleExpanded();
		}
	}
}
