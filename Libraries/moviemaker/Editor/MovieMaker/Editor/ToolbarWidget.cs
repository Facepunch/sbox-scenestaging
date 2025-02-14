using System.Linq;
using System.Reflection;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;


public class ToolbarWidget : Widget
{
	public Session Session { get; }
	public MovieEditor Editor { get; }

	public Layout EditModeControls { get; }

	private ComboBox PlayerDropdown { get; }
	private ComboBox ClipDropDown { get; }

	public ToolbarWidget( MovieEditor parent ) : base( parent )
	{
		Editor = parent;
		Session = parent.Session;

		Layout = Layout.Row();
		Layout.Spacing = 2;
		Layout.Margin = 4;

		{
			PlayerDropdown = new ComboBox( this );
			PlayerDropdown.MinimumWidth = 200;
			PlayerDropdown.ToolTip = $"Selected {nameof(MoviePlayer)} component";

			Layout.Add( PlayerDropdown );
		}

		Layout.AddSpacingCell( 4f );

		{
			ClipDropDown = new ComboBox( this );
			ClipDropDown.MinimumWidth = 150;
			ClipDropDown.ToolTip = $"Selected {nameof(MovieClip)}";

			Layout.Add( ClipDropDown );
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
		PlayerDropdown.Clear();

		foreach ( var player in playersAvailable.OrderBy( x => x.GameObject.Name ) )
		{
			PlayerDropdown.AddItem( $"{player.GameObject.Name}", "movie", () => Editor.Switch( player ), null, player == Session.Player );
		}

		PlayerDropdown.AddItem( "Create New..", "movie_filter", () => Editor.CreateNew() );
	}

	internal void UpdateClips()
	{
		ClipDropDown.Clear();

		ClipDropDown.AddItem( "Embedded", "attachment", () => Editor.SwitchToEmbedded(), "Use a clip stored in the player component.", Session?.Clip == Session?.Player.EmbeddedClip );

		var icon = typeof(MovieFile).GetCustomAttribute<GameResourceAttribute>()!.Icon;

		foreach ( var file in ResourceLibrary.GetAll<MovieFile>().OrderBy( x => x.ResourcePath ) )
		{
			ClipDropDown.AddItem( file.ResourceName, icon, () => Editor.SwitchFile( file ), file.ResourcePath, Session?.Clip == file.Clip );
		}

		ClipDropDown.AddItem( "Save As..", "save_as", Editor.SaveFileAs, "Save the current clip as a new movie file." );
	}
}

