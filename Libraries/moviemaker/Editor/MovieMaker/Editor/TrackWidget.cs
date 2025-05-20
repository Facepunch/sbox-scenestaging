using Editor.NodeEditor;
using Sandbox.MovieMaker;
using Sandbox.UI;
using System.Reflection;

namespace Editor.MovieMaker;

#nullable enable

public partial class TrackWidget : Widget
{
	public TrackListPage TrackList { get; }
	public new TrackWidget? Parent { get; }

	public new IEnumerable<TrackWidget> Children => _children;

	public TrackView View { get; }

	RealTimeSince _timeSinceInteraction = 1000;

	private readonly Label? _label;
	private readonly Button _collapseButton;
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

			var defaultColor = Theme.WidgetBackground.LerpTo( Theme.ControlBackground, canModify ? 0f : 0.5f );
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
		throw new NotImplementedException();
	}

	void LockChildren()
	{
		throw new NotImplementedException();
	}

	void UnlockChildren()
	{
		throw new NotImplementedException();
	}
}

file sealed class LockButton : Button
{
	public TrackWidget TrackWidget { get; }

	public LockButton( TrackWidget trackWidget )
	{
		TrackWidget = trackWidget;

		FixedSize = 24f;
	}

	protected override void OnPaint()
	{
		Paint.SetBrushAndPen( PaintExtensions.PaintSelectColor( Timeline.Colors.Background,
			Theme.ControlBackground.Lighten( 0.5f ), Theme.Primary ) );
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
	}

	protected override void OnPaint()
	{
		Paint.SetBrushAndPen( PaintExtensions.PaintSelectColor( Timeline.Colors.Background,
			Theme.ControlBackground.Lighten( 0.5f ), Theme.Primary ) );
		Paint.DrawRect( LocalRect, 4f );

		Paint.SetPen( Theme.TextControl );
		Paint.DrawIcon( LocalRect, Track.View.IsExpanded ? "remove" : "add", 12f );
	}

	protected override void OnClicked()
	{
		Track.View.IsExpanded = !Track.View.IsExpanded;
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
