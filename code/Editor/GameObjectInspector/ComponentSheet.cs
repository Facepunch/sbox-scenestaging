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
	Guid GameObjectId;
	
	string ExpandedCookieString => $"expand.{GameObjectId}.{TargetObject.TypeName}";

	/// <summary>
	/// The user's local preference to having this component expanded or not.
	/// </summary>
	bool ExpandedCookie
	{
		get => ProjectCookie.Get( ExpandedCookieString, true );
		set
		{
			// Don't bother storing the cookie if it's an expanded component
			if ( value )
			{
				ProjectCookie.Remove( ExpandedCookieString );
			}
			else
			{
				ProjectCookie.Set( ExpandedCookieString, value );
			}
		}
	}

	/// <summary>
	/// Is this component currently expanded?
	/// </summary>
	internal bool Expanded { get; set; } = true;

	/// <summary>
	/// Expands/shrinks the component in the component list.
	/// </summary>
	/// <param name="expanded"></param>
	internal void SetExpanded( bool expanded )
	{
		Expanded = expanded;
		RebuildContent();
		ExpandedCookie = expanded;
	}

	public ComponentSheet( Guid gameObjectId, SerializedObject target, Action<Vector2?> contextMenu ) : base( null )
	{
		GameObjectId = gameObjectId;
		Name = "ComponentSheet";
		TargetObject = target;
		Layout = Layout.Column();
		SetSizeMode( SizeMode.Default, SizeMode.CanShrink );

		// Check to see if we have a cookie to say if the component isn't expanded
		Expanded = ExpandedCookie;

		var header = Layout.Add( new ComponentHeader( TargetObject, this ) );
		header.WantsContextMenu = contextMenu;

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

	[Event.Hotload]
	public void RebuildContent()
	{
		Content.Clear( true );

		BuildInstanceContent();
	}

	void BuildInstanceContent()
	{
		if ( !Expanded ) return;
	
		var props = TargetObject.Where( x => x.HasAttribute<PropertyAttribute>() )
									.OrderBy( x => x.SourceLine )
									.ThenBy( x => x.DisplayName )
									.ToArray();


		HashSet<string> handledGroups = new ( StringComparer.OrdinalIgnoreCase );

		// Add ungrouped first
		ControlSheet cs = null;
		foreach ( var prop in props.Where( x => string.IsNullOrWhiteSpace(  x.GroupName ) ) )
		{
			cs ??= new ControlSheet();
			cs.AddRow( prop );
		}

		if ( cs is not null )
		{
			Content.Add( cs );
		}

		// add groups
		foreach ( var prop in props.Where( x => !string.IsNullOrWhiteSpace( x.GroupName ) ) )
		{

			if ( handledGroups.Contains( prop.GroupName ) )
				continue;

			handledGroups.Add( prop.GroupName );
			AddGroup( Content, prop.GroupName, props.Where( x => x.GroupName == prop.GroupName ).ToArray() );
		}
	}

	private void AddGroup( Layout sheet, string groupName, SerializedProperty[] props )
	{
		GroupHeader headerWidget = new GroupHeader( this );
		sheet.AddSpacingCell( 2 );
		sheet.Add( headerWidget );

		headerWidget.Title = groupName;

		SerializedProperty skipProperty = null;
		bool closed = false;

		var toggleGroup = props.FirstOrDefault( x => x.HasAttribute<ToggleGroupAttribute>() && x.Name == groupName );
		if ( toggleGroup is not null )
		{
			toggleGroup.TryGetAttribute<ToggleGroupAttribute>( out var toggleAttr );
			if ( toggleGroup is not null )
			{
				skipProperty = toggleGroup;

				var enabler = ControlWidget.Create( toggleGroup );

				headerWidget.Title = toggleAttr.Label ?? groupName;
				headerWidget.AddToggle( enabler );

				if ( !toggleGroup.As.Bool ) closed = true;
			}
		}

		var widget = new Widget( this );
		
		var ps = new ControlSheet();
		ps.Margin = new Sandbox.UI.Margin( 0, 2, 16, 0 );

		foreach ( var prop in props )
		{
			if ( skipProperty == prop )
				continue;

			ps.AddRow( prop, 16 );
		}

		widget.Layout = ps;

		sheet.Add( widget );

		headerWidget.OnToggled += ( s ) =>
		{
			using var su = SuspendUpdates.For( this );

			if ( !s ) widget.Hide();
			else widget.Show();
		};

		if ( closed ) headerWidget.Toggle();

		headerWidget.CookieName = $"expand.{GameObjectId}.{TargetObject.TypeName}.{groupName}";
	}

}

file class GroupHeader : Widget
{
	Layout toggleLayout;
	ControlWidget toggleControl;

	public GroupHeader( Widget parent ) : base( parent )
	{
		FixedHeight = ControlWidget.ControlRowHeight;
		Layout = Layout.Row();
		Layout.AddSpacingCell( 20 );
		Layout.Spacing = 5;

		toggleLayout = Layout.AddColumn();

		Layout.AddStretchCell();
	}

	protected override Vector2 SizeHint() => new Vector2( 4096, ControlWidget.ControlRowHeight );

	public string Title { get; set; }


	internal void AddToggle( ControlWidget controlWidget )
	{
		controlWidget.FixedHeight = 18;
		controlWidget.FixedWidth = 18;

		toggleLayout.Add( controlWidget );
		toggleControl = controlWidget;

		if ( controlWidget is BoolControlWidget bcw )
		{
			bcw.Color = Theme.Green;
		}
	}

	protected override void OnMousePress( MouseEvent e )
	{
		base.OnMousePress( e );

		if (e.Button == MouseButtons.Left )
		{
			Toggle();
		}
	}

	protected override void OnDoubleClick( MouseEvent e )
	{
		e.Accepted = false;
	}

	protected override void OnPaint()
	{
		bool isChecked = false;

		if ( toggleControl is BoolControlWidget bcw )
		{
			isChecked = bcw.IsChecked;
		}

		//if ( false )
		{
			Paint.ClearPen();
			Paint.SetBrush( Theme.Black.WithAlpha( 0.2f ) );
			var lr = LocalRect.Shrink( 1, 0 );
			Paint.DrawRect( lr, 5 );
		}

		float spacing = 5;

		Paint.ClearBrush();
		Paint.SetPen( Color.White.WithAlpha( Paint.HasMouseOver ? 0.8f : (state ? 0.7f : 0.3f ) ) );

		if ( isChecked ) Paint.SetPen( Theme.Green.WithAlpha( Paint.HasMouseOver ? 0.8f : (state ? 0.6f : 0.3f) ) );

		Paint.DrawText( LocalRect.Shrink( toggleLayout.OuterRect.Right + spacing, 0, 0, 0 ), Title, TextFlag.LeftCenter );

		Paint.SetPen( Color.White.WithAlpha( Paint.HasMouseOver ? 0.4f : 0.1f ) );
		Paint.DrawIcon( LocalRect.Shrink( 4, 0 ), state ? "arrow_drop_down" : "arrow_right", 16, TextFlag.LeftCenter );
	}

	bool state = true;

	public void Toggle()
	{
		state = !state;
		OnToggled?.Invoke( state );

		if ( CookieName is not null )
		{
			ProjectCookie.Set( CookieName, state );
		}
	}

	public Action<bool> OnToggled;

	string _cookieName;

	public string CookieName 
	{ 
		get
		{
			return _cookieName;
		}

		set
		{
			_cookieName = value;

			var newState = ProjectCookie.Get<bool>( _cookieName, state );
			if ( newState == state ) return;

			Toggle();
		}
	}
}

file class ComponentHeader : Widget
{
	SerializedObject TargetObject { get; init; }
	ComponentSheet Sheet { get; set; }

	public Action<Vector2?> WantsContextMenu;

	Layout expanderRect;
	Layout iconRect;
	Layout textRect;
	Layout moreRect;

	public ComponentHeader( SerializedObject target, ComponentSheet parent ) : base( parent )
	{
		TargetObject = target;
		Sheet = parent;
		MouseTracking = true;

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

		Layout.AddStretchCell( 1 );

		moreRect = Layout.AddRow();
		moreRect.AddSpacingCell( 16 );

		Layout.AddSpacingCell( 16 );

		IsDraggable = true;
	}

	protected override void OnDragStart()
	{
		base.OnDragStart();

		if ( !TargetObject.TryGetProperty( "GameObject", out var goProp ) ) return;

		var go = goProp.GetValue<GameObject>( null );
		if ( go == null ) return;

		var drag = new Drag( Sheet );
		drag.Data.Object = go;
		drag.Execute();
	}

	protected override void OnPaint()
	{
		base.OnPaint();

		float opacity = 0.6f;
		Paint.Antialiasing = true;
		Paint.TextAntialiasing = true;

		if ( Paint.HasMouseOver )
		{
			//Paint.ClearPen();
			//Paint.SetBrushRadial( 30, 256, Theme.Blue.WithAlpha( 0.1f ), Theme.Blue.WithAlpha( 0.0f ) ) ;
			//Paint.DrawRect( LocalRect, 0 );
			opacity = 1.0f;
		}

		if ( !Sheet.Expanded )
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

		Paint.SetPen( Theme.White.WithAlpha( opacity * 0.5f ) );
		Paint.DrawIcon( expanderRect.InnerRect, Sheet.Expanded ? "keyboard_arrow_down" : "chevron_right", 16, TextFlag.Center );

		Paint.SetPen( Theme.Blue.WithAlpha( opacity ) );
		Paint.DrawIcon( iconRect.InnerRect, string.IsNullOrEmpty( TargetObject.TypeIcon ) ? "category" : TargetObject.TypeIcon, 14, TextFlag.Center ); 

		Paint.SetPen( Theme.Blue.Lighten( 0.1f ).WithAlpha( (Sheet.Expanded ? 0.9f : 0.6f) * opacity ) );
		Paint.SetDefaultFont( 8, 1000, false );
		Paint.DrawText( textRect.InnerRect, TargetObject.TypeTitle, TextFlag.LeftCenter );

		Paint.DrawIcon( moreRect.InnerRect, "more_horiz", 16, TextFlag.RightCenter );
	}

	protected override void OnMouseRightClick( MouseEvent e )
	{
		base.OnMouseRightClick( e );

		WantsContextMenu?.Invoke( null );
		e.Accepted = true;
	}

	protected override void OnMouseClick( MouseEvent e )
	{
		base.OnMouseClick( e );

		if ( e.LeftMouseButton )
		{
			if ( moreRect.InnerRect.IsInside( e.LocalPosition ) )
			{
				WantsContextMenu?.Invoke( ToScreen( moreRect.OuterRect.BottomLeft ) );
			}
			else
			{
				Sheet.SetExpanded( !Sheet.Expanded );
			}
		}
	}
}
