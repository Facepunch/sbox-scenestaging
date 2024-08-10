using Sandbox.MovieMaker;

namespace Editor.MovieMaker;


public class ToolbarWidget : Widget
{
	public Session Session { get; private set; }
	public MovieEditor Editor { get; private set; }

	ComboBox PlayerDropdown;

	public ToolbarWidget( MovieEditor parent ) : base( parent )
	{
		Editor = parent;
		Session = parent.Session;

		Layout = Layout.Row();
		Layout.Spacing = 2;
		Layout.Margin = 4;

		{
			PlayerDropdown = new ComboBox( this );
			PlayerDropdown.FixedWidth = 150;
			Layout.Add( PlayerDropdown );
		}

		{
			var btn = new IconButton( "radio_button_checked" );
			btn.ToolTip = "Keyframe Record";
			btn.IconSize = 16;
			btn.IsToggle = true;
			btn.Background = Color.Transparent;
			btn.BackgroundActive = Color.Transparent;
			btn.ForegroundActive = Theme.Red;
			btn.Bind( "IsActive" ).From( () => Session.KeyframeRecording, x => Session.KeyframeRecording = x );
			Layout.Add( btn );
		}

		{
			var btn = new IconButton( "play_arrow" );
			btn.ToolTip = "Toggle Play";
			btn.IsToggle = true;
			btn.IconSize = 16;
			btn.Background = Color.Transparent;
			btn.BackgroundActive = Color.Transparent;
			btn.ForegroundActive = Theme.Primary;
			btn.Bind( "IsActive" ).From( () => Session.Playing, x => Session.Playing = x );
			Layout.Add( btn );
		}

		{
			var btn = new IconButton( "all_inclusive" );
			btn.ToolTip = "Loop when reaching end";
			btn.IsToggle = true;
			btn.IconSize = 16;
			btn.Background = Color.Transparent;
			btn.BackgroundActive = Color.Transparent;
			btn.ForegroundActive = Theme.Primary;
			btn.Bind( "IsActive" ).From( () => Session.Loop, x => Session.Loop = x );
			Layout.Add( btn );
		}

		Layout.AddStretchCell();
	}

	protected override void OnPaint()
	{
		Paint.SetBrushAndPen( Theme.WidgetBackground );
		Paint.DrawRect( LocalRect );
	}

	internal void UpdatePlayers( List<MovieClipPlayer> playersAvailable )
	{
		foreach ( var player in playersAvailable )
		{
			PlayerDropdown.AddItem( player.GameObject.Name, "movie", () => Editor.Switch( player ), null, player.clip == Session.Clip );
		}

		PlayerDropdown.AddItem( "Create New..", "add_photo_alternate", () => Editor.CreateNew() );

	}
}

