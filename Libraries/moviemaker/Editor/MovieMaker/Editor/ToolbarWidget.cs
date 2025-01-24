using Sandbox.MovieMaker;

namespace Editor.MovieMaker;


public class ToolbarWidget : Widget
{
	public Session Session { get; }
	public MovieEditor Editor { get; }

	public Layout EditModeControls { get; }

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
			PlayerDropdown.FixedWidth = 250;
			Layout.Add( PlayerDropdown );
		}

		Layout.AddSpacingCell( 16f );

		foreach ( var type in EditMode.AllTypes )
		{
			var btn = new IconButton( type.Icon ) { ToolTip = type.Title, IsToggle = true, IconSize = 16 };

			btn.Bind( "IsActive" ).From( () => type.IsMatchingType( Session.EditMode ), x => Session.SetEditMode( type ) );

			Layout.Add( btn );
		}

		Layout.AddSpacingCell( 16f );

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
			var btn = new IconButton( "repeat" );
			btn.ToolTip = "Loop at End of Playback";
			btn.IsToggle = true;
			btn.IconSize = 16;
			btn.Background = Color.Transparent;
			btn.BackgroundActive = Color.Transparent;
			btn.ForegroundActive = Theme.Primary;
			btn.Bind( "IsActive" ).From( () => Session.Loop, x => Session.Loop = x );
			Layout.Add( btn );
		}

		EditModeControls = Layout.AddRow();
		EditModeControls.Spacing = 2;

		Layout.AddStretchCell();
	}

	protected override void OnPaint()
	{
		Paint.SetBrushAndPen( Theme.WidgetBackground );
		Paint.DrawRect( LocalRect );
	}

	internal void UpdatePlayers( List<MoviePlayer> playersAvailable )
	{
		foreach ( var player in playersAvailable )
		{
			PlayerDropdown.AddItem( player.GameObject.Name, "movie", () => Editor.Switch( player ), null, player.MovieClip == Session.Clip );
		}

		PlayerDropdown.AddItem( "Create New..", "add_photo_alternate", () => Editor.CreateNew() );

	}
}

