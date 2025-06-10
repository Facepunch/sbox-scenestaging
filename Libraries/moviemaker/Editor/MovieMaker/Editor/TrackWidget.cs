using System.Linq;
using Editor.NodeEditor;
using Sandbox.MovieMaker;
using Sandbox.UI;
using System.Reflection;
using Sandbox.MovieMaker.Properties;

namespace Editor.MovieMaker;

#nullable enable

/// <summary>
/// An item in the <see cref="TrackListPage"/>, showing the name of a track with buttons to configure it.
/// </summary>
public partial class TrackWidget : Widget
{
	public TrackListPage TrackList { get; }
	public new TrackWidget? Parent { get; }

	public new IEnumerable<TrackWidget> Children => _children;

	public TrackView View { get; }

	RealTimeSince _timeSinceInteraction = 1000;

	private readonly Label? _label;
	private readonly Button _collapseButton;
	private readonly Button _addButton;
	private readonly Button _lockButton;
	private readonly Layout _childLayout;
	private readonly SynchronizedSet<TrackView, TrackWidget> _children;

	private ControlWidget? _controlWidget;

	public TrackWidget( TrackListPage trackList, TrackWidget? parent, TrackView view )
		: base( (Widget?)parent ?? trackList )
	{
		TrackList = trackList;
		Parent = parent;

		View = view;
		FocusMode = FocusMode.TabOrClickOrWheel;
		VerticalSizeMode = SizeMode.CanGrow;

		_children = new SynchronizedSet<TrackView, TrackWidget>(
			AddChildTrack, RemoveChildTrack, UpdateChildTrack );

		ToolTip = View.Description;

		View.Changed += View_Changed;
		View.ValueChanged += View_ValueChanged;

		Layout = Layout.Column();

		var row = Layout.AddRow();

		row.Spacing = 4f;
		row.Margin = 4f;

		_childLayout = Layout.Add( Layout.Column() );
		_childLayout.Margin = new Margin( 8f, 0f, 0f, 0f );

		_collapseButton = new CollapseButton( this );
		row.Add( _collapseButton );

		if ( !AddReferenceControl( row ) )
		{
			row.AddSpacingCell( 8f );
			_label = row.Add( new Label( view.Target.Name ) );
		}

		row.AddStretchCell();

		if ( view.Track is not ProjectSequenceTrack && TrackProperty.GetAll( View.Target ).Any() )
		{
			_addButton = row.Add( new AddButton( this ) );
		}

		_lockButton = row.Add( new LockButton( this ) );

		View_Changed( view );
	}

	private TrackWidget AddChildTrack( TrackView source ) => new( TrackList, this, source );
	private void RemoveChildTrack( TrackWidget item ) => item.Destroy();
	private bool UpdateChildTrack( TrackView source, TrackWidget item ) => item.UpdateLayout();

	public bool UpdateLayout()
	{
		_children.Update( View.IsExpanded ? View.Children : [] );
		_childLayout.Clear( false );

		foreach ( var child in _children )
		{
			_childLayout.Add( child );
		}

		return true;
	}

	private bool AddReferenceControl( Layout layout )
	{
		if ( View.Target is not ITrackReference reference ) return false;
		if ( reference is { IsBound: true, Value: GameObject } && View.Parent is not null ) return false;

		// Add control to retarget a scene reference (Component / GameObject)

		_controlWidget = null;

		if ( View.Track is ProjectSequenceTrack )
		{
			//
		}
		else if ( reference is ITrackReference<GameObject> goReference )
		{
			_controlWidget = ControlWidget.Create( EditorTypeLibrary.CreateProperty( reference.Name,
				() => goReference.Value, goReference.Bind ) );
		}
		else
		{
			var helperType = typeof( ReflectionHelper<> ).MakeGenericType( reference.TargetType );
			var createControlMethod = helperType.GetMethod( nameof( ReflectionHelper<IValid>.CreateControlWidget ),
				BindingFlags.Static | BindingFlags.Public )!;

			_controlWidget = (ControlWidget)createControlMethod.Invoke( null, [View.Track, reference] )!;
		}

		if ( !_controlWidget.IsValid() ) return false;

		_controlWidget.MaximumWidth = 300;

		layout.Add( _controlWidget );
		return true;
	}

	private void View_Changed( TrackView view )
	{
		var labelColor = new Color( 0.6f, 0.6f, 0.6f );

		_collapseButton.Visible = view.Children.Count > 0;

		_lockButton.Update();
		_collapseButton.Update();

		if ( _controlWidget is not null )
		{
			_controlWidget.Enabled = !View.IsLocked;
		}

		if ( _label is not null )
		{
			_label.Color = !View.IsLocked ? IsSelected ? Color.White : labelColor : labelColor.Darken( 0.25f );
		}

		Update();
	}

	private void View_ValueChanged( TrackView view )
	{
		_timeSinceInteraction = 0f;
		Update();
	}

	public override void OnDestroyed()
	{
		View.Changed -= View_Changed;
		View.ValueChanged -= View_ValueChanged;

		base.OnDestroyed();
	}

	protected override void OnMouseClick( MouseEvent e )
	{
		base.OnMouseClick( e );

		if ( e.LeftMouseButton )
		{
			View.Select();

			e.Accepted = true;
		}
	}

	protected override void OnDoubleClick( MouseEvent e )
	{
		View.InspectProperty();

		e.Accepted = true;
	}

	protected override Vector2 SizeHint()
	{
		return 32;
	}

	public bool IsSelected => View.IsSelected;

	public Color BackgroundColor
	{
		get
		{
			var canModify = !View.IsLocked;

			var defaultColor = Theme.SurfaceBackground.LerpTo( Theme.ControlBackground, canModify ? 0f : 0.5f );
			var hoveredColor = defaultColor.Lighten( 0.25f );
			var selectedColor = Color.Lerp( defaultColor, Theme.Primary, canModify ? 0.5f : 0.2f );

			var isHovered = canModify && IsUnderMouse;

			return IsSelected ? selectedColor
				: isHovered ? hoveredColor
					: defaultColor;
		}
	}

	protected override void OnPaint()
	{
		Paint.Antialiasing = false;
		Paint.SetBrushAndPen( BackgroundColor );
		Paint.DrawRect( new Rect( LocalRect.Left + 1f, LocalRect.Top + 1f, LocalRect.Width - 2f, Timeline.TrackHeight - 2f ), 4 );

		if ( _timeSinceInteraction < 2.0f )
		{
			var delta = _timeSinceInteraction.Relative.Remap( 2.0f, 0, 0, 1 );
			Paint.SetBrush( Theme.Yellow.WithAlpha( delta ) );
			Paint.DrawRect( new Rect( LocalRect.Right - 4, LocalRect.Top, 32, Timeline.TrackHeight ) );
			Update();
		}
	}

	private Menu? _menu;

	protected override void OnContextMenu( ContextMenuEvent e )
	{
		e.Accepted = true;

		ShowContextMenu();
	}

	public void ShowContextMenu()
	{
		_menu = new Menu( this );

		if ( View.Track is ProjectSequenceTrack sequenceTrack )
		{
			var rename = _menu.AddMenu( "Rename", "edit" );

			rename.AddLineEdit( "Name", sequenceTrack.Name, autoFocus: true, onSubmit: OnRename );
		}

		_menu.AddOption( "Delete", "delete", Remove );

		if ( View.Children.Count > 0 )
		{
			_menu.AddOption( "Delete Empty", "cleaning_services", RemoveEmptyChildren );
			_menu.AddSeparator();
			_menu.AddOption( "Lock Children", "lock", LockChildren );
			_menu.AddOption( "Unlock Children", "lock_open", UnlockChildren );
		}

		_menu.OpenAtCursor();
	}
	private record AvailableTrackProperty( string Name, string Category, Type Type, Action Create );

	public void ShowAddMenu( Vector2 openPos )
	{
		_menu = new Menu( this );

		var session = TrackList.Session;
		var availableTracks = new List<AvailableTrackProperty>();

		if ( View.Target is ITrackReference<GameObject> { IsBound: true, Value: { Components.Count: > 0 } go } )
		{
			foreach ( var component in go.Components.GetAll() )
			{
				var type = component.GetType();

				availableTracks.Add( new AvailableTrackProperty( type.Name, "Components", type,
					() => session.GetOrCreateTrack( component ) ) );
			}
		}

		foreach ( var property in TrackProperty.GetAll( View.Target ) )
		{
			availableTracks.Add( new AvailableTrackProperty( property.Name, property.Category, property.Type,
				() => session.GetOrCreateTrack( View.Track, property.Name ) ) );
		}

		var categories = availableTracks.GroupBy( x => x.Category ).ToArray();

		foreach ( var category in categories.OrderBy( x => x.Key ) )
		{
			var subMenu = categories.Length == 1 ? _menu : _menu.AddMenu( category.Key );

			foreach ( var type in category.GroupBy( x => x.Type.ToSimpleString( false ) ).OrderBy( x => x.Key ) )
			{
				if ( category.Key != "Components" )
				{
					subMenu.AddHeading( type.Key ).Color = Theme.TextDisabled;
				}

				foreach ( var item in type.OrderBy( x => x.Name ) )
				{
					var option = new ToggleOption( item.Name, View.Children.Any( x => x.Track.Name == item.Name ), create =>
					{
						using var scope = session.History.Push( $"{(create ? "Create" : "Remove")} Track ({item.Name})" );

						if ( create )
						{
							item.Create();
						}
						else
						{
							View.Children
								.FirstOrDefault( x => x.Track.Name == item.Name )?
								.Remove();
						}

						session.TrackList.Update();
						session.ClipModified();
					} );

					subMenu.AddWidget( option );
				}
			}
		}

		_menu.OpenAt( openPos, false );
	}

	private void OnRename( string name )
	{
		if ( View.Track is not ProjectSequenceTrack sequenceTrack ) return;

		sequenceTrack.Name = name;

		if ( _label is { } label ) label.Text = name;
	}

	void Remove()
	{
		using var scope = TrackList.Session.History.Push( "Remove Track(s)" );
		View.Remove();
	}

	void RemoveEmptyChildren()
	{
		foreach ( var child in View.Children.ToArray() )
		{
			RemoveEmptyCore( child );
		}

		TrackList.Session.TrackList.Update();
	}

	private static bool RemoveEmptyCore( TrackView view )
	{
		var allChildrenRemoved = true;

		foreach ( var child in view.Children.ToArray() )
		{
			allChildrenRemoved &= RemoveEmptyCore( child );
		}

		if ( allChildrenRemoved && view.IsEmpty )
		{
			view.Remove();
			return true;
		}

		return false;
	}

	void LockChildren()
	{
		foreach ( var child in View.Children )
		{
			child.IsLockedSelf = true;
		}
	}

	void UnlockChildren()
	{
		foreach ( var child in View.Children )
		{
			child.IsLockedSelf = false;
		}
	}
}

// TODO: surely there's an easier way to stop Menus from closing
file sealed class ToggleOption : Widget
{
	private readonly Label _label;
	private readonly Action<bool> _toggled;

	private bool _isActive;

	public bool IsActive
	{
		get => _isActive;
		set
		{
			_isActive = value;

			_label.SetStyles( value ? "font-weight: bold;" : "font-weight: regular;" );
		}
	}

	protected override Vector2 SizeHint()
	{
		// So there's enough space for the label to become bold

		return base.SizeHint() * new Vector2( 1.1f, 1f );
	}

	public ToggleOption( string title, bool active, Action<bool> toggled )
	{
		Layout = Layout.Row();
		Layout.Margin = new Margin( 40f, 5f, 16f, 5f );

		_label = new Label( title, this );
		_toggled = toggled;

		MinimumWidth = 120f;

		Layout.Add( _label );

		IsActive = active;
	}

	protected override void OnPaint()
	{
		if ( Paint.HasMouseOver )
		{
			Paint.SetBrushAndPen( Theme.SurfaceBackground );
			Paint.DrawRect( LocalRect.Shrink( IsActive ? 0f : 4f, 0f, 4f, 0f ), 3f );
		}

		if ( IsActive )
		{
			Paint.SetBrushAndPen( Theme.Primary );
			Paint.DrawRect( LocalRect.Contain( new Vector2( 3f, LocalRect.Height ), TextFlag.LeftCenter ) );

			Paint.SetPen( Theme.Text );
			Paint.DrawIcon( LocalRect.Shrink( 16f ), "done", 13f, TextFlag.LeftCenter );
		}
	}

	protected override void OnMouseReleased( MouseEvent e )
	{
		base.OnMouseReleased( e );

		IsActive = !IsActive;

		e.Accepted = true;

		_toggled.Invoke( IsActive );
		Update();
	}
}

file sealed class LockButton : Button
{
	public TrackWidget TrackWidget { get; }

	public LockButton( TrackWidget trackWidget )
	{
		TrackWidget = trackWidget;

		FixedSize = 24f;

		ToolTip = "Toggle lock";
	}

	protected override void OnPaint()
	{
		Paint.SetBrushAndPen( PaintExtensions.PaintSelectColor( Theme.ControlBackground,
			Theme.ControlBackground.Darken( 0.5f ), Theme.Primary ) );
		Paint.DrawRect( LocalRect, 4f );

		Paint.SetPen( Theme.TextControl );
		Paint.DrawIcon( LocalRect, TrackWidget.View.IsLockedSelf ? "lock" : "lock_open", 12f );
	}

	protected override void OnClicked()
	{
		using var scope = TrackWidget.TrackList.Session.History.Push( $"{(TrackWidget.View.IsLockedSelf ? "Unlocked" : "Locked")} Track" );
		TrackWidget.View.IsLockedSelf = !TrackWidget.View.IsLockedSelf;
	}
}

file sealed class CollapseButton : Button
{
	public TrackWidget Track { get; }

	public CollapseButton( TrackWidget track )
	{
		Track = track;
		FixedSize = 24f;

		ToolTip = "Toggle expanded";
	}

	protected override void OnPaint()
	{
		Paint.SetBrushAndPen( PaintExtensions.PaintSelectColor( Theme.ControlBackground,
			Theme.ControlBackground.Darken( 0.5f ), Theme.Primary ) );
		Paint.DrawRect( LocalRect, 4f );

		Paint.SetPen( Theme.TextControl );
		Paint.DrawIcon( LocalRect, Track.View.IsExpanded ? "remove" : "add", 12f );
	}

	protected override void OnClicked()
	{
		Track.View.IsExpanded = !Track.View.IsExpanded;
	}
}

file sealed class AddButton : Button
{
	public TrackWidget Track { get; }

	public AddButton( TrackWidget track )
	{
		Track = track;
		FixedSize = 24f;

		ToolTip = "Add sub-track";
	}

	protected override void OnPaint()
	{
		Paint.SetBrushAndPen( PaintExtensions.PaintSelectColor(Theme.ControlBackground,
			Theme.ControlBackground.Darken( 0.5f ), Theme.Primary ) );
		Paint.DrawRect( LocalRect, 4f );

		Paint.SetPen( Theme.TextControl );
		Paint.DrawIcon( LocalRect, "playlist_add", 12f );
	}

	protected override void OnClicked()
	{
		Track.ShowAddMenu( Application.CursorPosition );
	}
}

file sealed class ReflectionHelper<T>
	where T : class, IValid
{
	public static ControlWidget CreateControlWidget( IProjectReferenceTrack track, ITrackReference<T> target )
	{
		return ControlWidget.Create( EditorTypeLibrary.CreateProperty( target.Name,
			() => target.Value, value =>
			{
				track.ReferenceId = value switch
				{
					Component cmp => cmp.Id,
					GameObject go => go.Id,
					_ => null
				};

				target.Bind( value );
			} ) );
	}
}
