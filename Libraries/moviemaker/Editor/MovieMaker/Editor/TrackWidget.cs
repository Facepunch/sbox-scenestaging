
using Sandbox.MovieMaker;
using Sandbox.MovieMaker.Tracks;

namespace Editor.MovieMaker;


public class TrackWidget : Widget
{
	public TrackListWidget TrackList;
	public MovieTrack Source;

	public DopesheetTrack Channel { get; set; }

	RealTimeSince timeSinceInteraction = 1000;

	public TrackWidget( MovieTrack source, TrackListWidget list ) : base()
	{
		TrackList = list;
		Source = source;
		FocusMode = FocusMode.TabOrClickOrWheel;
		VerticalSizeMode = SizeMode.CanGrow;

		Layout = Layout.Row();
		Layout.Margin = new Sandbox.UI.Margin( 4, 4, 32, 4 );

		if ( source is PropertyTrack pt )
		{
			var so = pt.GetSerialized();

			if ( pt.Component is not null )
			{
				var ctrl = ControlWidget.Create( so.GetProperty( nameof( PropertyTrack.Component ) ) );
				if ( ctrl is not null )
				{
					ctrl.MaximumWidth = 300;
					Layout.Add( ctrl );
				}
			}
			else
			{
				var ctrl = ControlWidget.Create( so.GetProperty( nameof( PropertyTrack.GameObject ) ) );
				if ( ctrl is not null )
				{
					ctrl.MaximumWidth = 300;
					Layout.Add( ctrl );
				}
			}

			Layout.AddSpacingCell( 16 );
			Layout.Add( new Label( pt.PropertyName ) );
		}
	}

	public override void OnDestroyed()
	{
		base.OnDestroyed();

		Channel?.Destroy();
		Channel = default;
	}

	protected override Vector2 SizeHint()
	{
		return 32;
	}

	protected override void OnPaint()
	{
		var bg = Extensions.PaintSelectColor( TrackDopesheet.Colors.ChannelBackground, TrackDopesheet.Colors.ChannelBackground.Lighten( 0.1f ), Theme.Primary );

		if ( menu.IsValid() && menu.Visible )
			bg = Color.Lerp( TrackDopesheet.Colors.ChannelBackground, Theme.Primary, 0.2f );

		Paint.Antialiasing = false;
		Paint.SetBrushAndPen( bg );
		Paint.DrawRect( new Rect( LocalRect.Left, LocalRect.Top, LocalRect.Width + 100, LocalRect.Height ), 4 );

		if ( timeSinceInteraction < 2.0f )
		{
			var delta = timeSinceInteraction.Relative.Remap( 2.0f, 0, 0, 1 );
			Paint.SetBrush( Theme.Yellow.WithAlpha( delta ) );
			Paint.DrawRect( new Rect( LocalRect.Right - 4, LocalRect.Top, 32, LocalRect.Height ) );
			Update();
		}
	}

	protected override void DoLayout()
	{
		base.DoLayout();

		var pos = Channel.GraphicsView.FromScreen( ScreenPosition );

		Channel.DoLayout( new Rect( pos, Size ) );
	}

	internal void AddKey( float currentPointer )
	{
		Channel.AddKey( currentPointer );
	}

	/// <summary>
	/// Write data from this widget to the Clip
	/// </summary>
	public void Write()
	{
		Channel.Write();
	}

	internal void AddKey( float time, object value )
	{
		Channel.AddKey( time, value );
	}

	Menu menu;

	protected override void OnContextMenu( ContextMenuEvent e )
	{
		menu = new Menu( this );
		menu.AddOption( "Delete", "delete", RemoveTrack );
		menu.OpenAtCursor();
	}

	void RemoveTrack()
	{
		TrackList.Session.Clip.RemoveTrack( Source );
		TrackList.RebuildTracksIfNeeded();
	}

	public void NoteInteraction()
	{
		timeSinceInteraction = 0;
		Update();
	}
}

