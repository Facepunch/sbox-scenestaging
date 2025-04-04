using System.Linq;
using Sandbox.MovieMaker;
using System.Reflection;
using Sandbox.Resources;
using Sandbox.UI;
using Sandbox;

namespace Editor.MovieMaker;

#nullable enable

public class MovieEditorPanel : Widget
{
	public MovieEditor Editor { get; }
	public ToolbarWidget ToolBar { get; }

	public MovieEditorPanel( MovieEditor parent )
		: base( parent )
	{
		Editor = parent;
		Layout = Layout.Column();

		ToolBar = new ToolbarWidget( this );

		Layout.Add( ToolBar );
	}
}

public sealed class ListPanel : MovieEditorPanel
{
	public TrackListWidget TrackList { get; }

	public ListPanel( MovieEditor parent, Session session )
		: base( parent )
	{
		TrackList = new TrackListWidget( this, session );

		Layout.Add( TrackList );

		MinimumWidth = 300;

		// File menu

		var fileGroup = ToolBar.AddGroup( true );
		var resourceIcon = typeof( MovieResource ).GetCustomAttribute<GameResourceAttribute>()!.Icon;

		var fileAction = fileGroup.AddAction( "File", "folder", () =>
		{
			var menu = new Menu();

			menu.AddHeading( "File" );

			menu.AddOption( "New Movie", "note_add", Editor.SwitchToNewEmbedded );

			menu.AddSeparator();

			var openMenu = menu.AddMenu( "Open Movie", "folder_open" );

			var movies = ResourceLibrary.GetAll<MovieResource>().ToArray();

			openMenu.AddOptions( movies, x => $"{x.ResourcePath}:{resourceIcon}", Editor.SwitchResource );

			var importMenu = menu.AddMenu( "Import Movie", "folder_open" );

			importMenu.AddOptions( movies, x => $"{x.ResourcePath}:{resourceIcon}", x =>
			{
				session.GetOrCreateTrack( x );
				session.TrackList.Update();
			} );

			menu.AddSeparator();

			menu.AddOption( $"Save Movie", "save", parent.OnSave, shortcut: "CTRL+S" );

			var saveAsMenu = menu.AddMenu( $"Save Movie As..", "save_as" );

			var embed = saveAsMenu.AddOption( "Embedded", "attach_file", parent.SwitchToEmbedded );

			embed.Checkable = true;
			embed.Checked = session.Resource is EmbeddedMovieResource;
			embed.ToolTip = "Store the movie inside this Movie Player component, embedded in the current scene or prefab.";

			saveAsMenu.AddOption( "New Movie Resource", resourceIcon, parent.SaveFileAs );

			menu.AddSeparator();

			var playerMenu = menu.AddMenu( "Select Player", "movie" );
			var scene = SceneEditorSession.Active?.Scene;
			var players = scene?.GetAllComponents<MoviePlayer>() ?? [];

			foreach ( var player in players )
			{
				var option = playerMenu.AddOption( player.GameObject.Name, "movie", () => Editor.Switch( player ) );

				option.Checkable = true;
				option.Checked = session.Player == player;
			}

			playerMenu.AddOption( "Create New..", "movie_filter", Editor.CreateNewPlayer );

			menu.OpenAt( fileGroup.ScreenRect.BottomLeft );
		} );

		fileAction.ToolTip = "File menu for opening, importing, or saving movie projects.";

		// File name label

		var resourceGroup = ToolBar.AddGroup( true );

		var label = resourceGroup.AddLabel( GetFullPath( session ) );

		label.Alignment = TextFlag.Center;
		label.HorizontalSizeMode = SizeMode.CanGrow | SizeMode.Expand;
		label.ToolTip = session.Resource is MovieResource res ? res.ResourcePath : "";

		// Navigation buttons

		var navigateGroup = ToolBar.AddGroup( true );

		navigateGroup.AddAction( "Back", "arrow_back", parent.ExitSequence,
			() => parent.Session?.Parent is not null );
	}

	private static string GetFullPath( Session session )
	{
		var name = session.Resource is MovieResource res ? res.ResourceName.ToTitleCase() : "Embedded";

		return session.Parent is { } parent
			? $"{GetFullPath( parent )} → {name}"
			: name;
	}
}

public sealed class DopeSheetPanel : MovieEditorPanel
{
	public DopeSheet DopeSheet { get; }

	public DopeSheetPanel( MovieEditor parent, Session session )
		: base( parent )
	{
		DopeSheet = new DopeSheet( session );

		MouseTracking = true;

		Layout.Add( DopeSheet );

		var playbackGroup = ToolBar.AddGroup( true );

		playbackGroup.AddToggle( "Toggle Record", "radio_button_checked",
			() => session.IsRecording, x => session.IsRecording = x,
			background: false ).ForegroundActive = Theme.Red;

		playbackGroup.AddToggle( "Toggle Play", "play_arrow",
			() => session.IsPlaying, x => session.IsPlaying = x,
			background: false );

		playbackGroup.AddToggle( "Loop at End of Playback", "repeat",
			() => session.IsLooping, x => session.IsLooping = x,
			background: false );

		var slider = new FloatSlider( null )
		{
			ToolTip = "Playback Rate",
			FixedWidth = 80f,
			Minimum = 0f,
			Maximum = 2f,
			Step = 0.1f
		};

		slider.Bind( nameof( FloatSlider.Value ) )
			.From( () => session.TimeScale, value => session.TimeScale = value );

		playbackGroup.Layout.Add( slider );

		var speed = new Label( null )
		{
			Color = Color.White.Darken( 0.5f ),
			FixedWidth = 30f,
			Margin = 4f,
			Alignment = TextFlag.Center
		};

		speed.Bind( nameof( Label.Text ) )
			.ReadOnly()
			.From( () => $"x{session.TimeScale:F1}", null );

		slider.MouseRightClick += () => session.TimeScale = 1f;
		speed.MouseRightClick += () => session.TimeScale = 1f;

		playbackGroup.Layout.Add( speed );

		var snapGroup = ToolBar.AddGroup( true, true );

		snapGroup.AddToggle( "Object Snap", "align_horizontal_left", 
			() => session.ObjectSnap, x => session.ObjectSnap = x,
			background: false );

		snapGroup.AddToggle( "Frame Snap", "straighten",
			() => session.FrameSnap, x => session.FrameSnap = x,
			background: false );

		snapGroup.AddSpacingCell();

		var rate = new ComboBox { ToolTip = "Snap Frame Rate" };

		foreach ( var frameRate in MovieTime.SupportedFrameRates )
		{
			rate.AddItem( $"{frameRate} FPS", onSelected: () => session.FrameRate = frameRate,
				selected: session.FrameRate == frameRate );
		}

		snapGroup.Layout.Add( rate );
	}
}
