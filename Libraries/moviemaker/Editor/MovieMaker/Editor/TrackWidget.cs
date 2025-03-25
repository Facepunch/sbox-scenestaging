using System.Linq;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

public partial class TrackWidget : Widget
{
	public TrackListWidget TrackList { get; }
	public TrackWidget? Parent { get; }

	public new IEnumerable<TrackWidget> Children => base.Children.OfType<TrackWidget>();

	public ITrackView View { get; }

	public Layout Buttons { get; }

	RealTimeSince _timeSinceInteraction = 1000;

	private readonly Label _label;
	private readonly Button _lockButton;

	public TrackWidget( TrackListWidget trackList, TrackWidget? parent, ITrackView view )
		: base( (Widget?)parent ?? trackList )
	{
		TrackList = trackList;
		Parent = parent;

		View = view;
		FocusMode = FocusMode.TabOrClickOrWheel;
		VerticalSizeMode = SizeMode.CanGrow;

		View.Changed += View_Changed;

		Layout = Layout.Row();
		Layout.Spacing = 12f;
		Layout.Margin = 4f;

		Buttons = Layout.AddRow();
		Buttons.Spacing = 2f;
		Buttons.Margin = 2f;

		_lockButton = Buttons.Add( new LockButton( this ) );
		_label = Layout.Add( new Label( view.Target.Name ) );

		if ( View.Target is ITrackReference reference )
		{
			if ( reference is { IsBound: true, Value: GameObject } && View.Parent is not null )
			{
				return;
			}

			// Add control to retarget a scene reference (Component / GameObject)

			ControlWidget ctrl;

			if ( reference is ITrackReference<GameObject> goReference )
			{
				ctrl = ControlWidget.Create( EditorTypeLibrary.CreateProperty( reference.Name,
					() => goReference.Value, goReference.Bind ) );
			}
			else
			{
				ctrl = ControlWidget.Create( EditorTypeLibrary.CreateProperty( reference.Name,
					() => (Component?)reference.Value, reference.Bind ) );
			}

			if ( ctrl.IsValid() )
			{
				ctrl.MaximumWidth = 300;
				Layout.Add( ctrl );
			}
		}

		UpdateLockedState();
	}

	private void View_Changed( ITrackView view )
	{
		_timeSinceInteraction = 0f;

		UpdateLockedState();
	}

	public override void OnDestroyed()
	{
		View.Changed -= View_Changed;

		base.OnDestroyed();
	}

	private void UpdateLockedState()
	{
		Update();

		var labelColor = new Color( 0.6f, 0.6f, 0.6f );

		_lockButton.Update();
		_label.Color = !View.IsLocked ? labelColor : labelColor.Darken( 0.5f );
	}

	protected override void OnDoubleClick( MouseEvent e )
	{
		View.InspectProperty();
	}

	protected override Vector2 SizeHint()
	{
		return 32;
	}

	public Color BackgroundColor
	{
		get
		{
			var canModify = !View.IsLocked;

			var defaultColor = DopeSheet.Colors.ChannelBackground.Lighten( canModify ? 0f : 0.2f );
			var hoveredColor = defaultColor.Lighten( 0.1f );
			var selectedColor = Color.Lerp( defaultColor, Theme.Primary, canModify ? 0.5f : 0.2f );

			var isHovered = IsUnderMouse;
			var isSelected = IsFocused || _menu.IsValid() && _menu.Visible;

			return isSelected ? selectedColor
				: isHovered ? hoveredColor
					: defaultColor;
		}
	}

	protected override void OnPaint()
	{
		Paint.Antialiasing = false;
		Paint.SetBrushAndPen( BackgroundColor );
		Paint.DrawRect( new Rect( LocalRect.Left, LocalRect.Top, LocalRect.Width, LocalRect.Height ), 4 );

		if ( _timeSinceInteraction < 2.0f )
		{
			var delta = _timeSinceInteraction.Relative.Remap( 2.0f, 0, 0, 1 );
			Paint.SetBrush( Theme.Yellow.WithAlpha( delta ) );
			Paint.DrawRect( new Rect( LocalRect.Right - 4, LocalRect.Top, 32, LocalRect.Height ) );
			Update();
		}
	}

	private Menu? _menu;

	protected override void OnContextMenu( ContextMenuEvent e )
	{
		_menu = new Menu( this );
		_menu.AddOption( "Delete", "delete", View.Remove );

		if ( View.HasChildren )
		{
			_menu.AddOption( "Delete Empty", "cleaning_services", RemoveEmptyChildren );
			_menu.AddSeparator();
			_menu.AddOption( "Lock Children", "lock", LockChildren );
			_menu.AddOption( "Unlock Children", "lock_open", UnlockChildren );
		}

		_menu.OpenAtCursor();
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

file class LockButton : Button
{
	public TrackWidget TrackWidget { get; }

	public LockButton( TrackWidget trackWidget )
	{
		TrackWidget = trackWidget;

		FixedSize = 20f;
	}

	protected override void OnPaint()
	{
		Paint.SetBrushAndPen( PaintExtensions.PaintSelectColor( DopeSheet.Colors.Background,
			Theme.ControlBackground.Lighten( 0.5f ), Theme.Primary ) );
		Paint.DrawRect( LocalRect, 4f );

		Paint.SetPen( Theme.ControlText );
		Paint.DrawIcon( LocalRect, TrackWidget.View.IsLockedSelf ? "lock" : "lock_open", 12f );
	}

	protected override void OnClicked()
	{
		TrackWidget.View.IsLockedSelf = !TrackWidget.View.IsLockedSelf;
	}
}

file class CollapseButton : Button
{
	public TrackWidget Track { get; }

	public CollapseButton( TrackWidget track )
	{
		Track = track;
		FixedSize = 20f;
	}

	protected override void OnPaint()
	{
		Paint.SetBrushAndPen( PaintExtensions.PaintSelectColor( DopeSheet.Colors.Background,
			Theme.ControlBackground.Lighten( 0.5f ), Theme.Primary ) );
		Paint.DrawRect( LocalRect, 4f );

		Paint.SetPen( Theme.ControlText );
		Paint.DrawIcon( LocalRect, Track.View.IsExpanded ? "remove" : "add", 12f );
	}

	protected override void OnClicked()
	{
		Track.View.IsExpanded = !Track.View.IsExpanded;
	}
}
