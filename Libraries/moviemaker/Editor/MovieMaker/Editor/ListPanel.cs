using System.Linq;
using Sandbox.MovieMaker.Compiled;
using Sandbox.MovieMaker;
using System.Reflection;

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

	private ComboBox PlayerDropdown { get; }

	public ListPanel( MovieEditor parent, Session session )
		: base( parent )
	{
		TrackList = new TrackListWidget( this, session );

		Layout.Add( TrackList );

		MinimumWidth = 300;

		var fileGroup = ToolBar.AddGroup( true );
		var resourceIcon = typeof( MovieResource ).GetCustomAttribute<GameResourceAttribute>()!.Icon;

		fileGroup.AddAction( "Open Movie", "file_open", () =>
		{
			var menu = new Menu();

			menu.AddHeading( "Open Movie" );

			foreach ( var resource in ResourceLibrary.GetAll<MovieResource>().OrderBy( x => x.ResourcePath ) )
			{
				var option = menu.AddOption( resource.ResourcePath, resourceIcon, () => Editor.SwitchResource( resource ) );

				option.Checkable = true;
				option.Checked = session.Resource == resource;
			}

			menu.OpenAtCursor();
		} );

		fileGroup.AddAction( "Save Movie", "save", parent.OnSave, () => session.HasUnsavedChanges );
		fileGroup.AddAction( "Save Movie As..", "save_as", () =>
		{
			var menu = new Menu();

			menu.AddHeading( "Save Movie As.." );

			var embed = menu.AddOption( "Embedded", "attach_file", parent.SwitchToEmbedded );

			embed.Checkable = true;
			embed.Checked = session.Resource is EmbeddedMovieResource;
			embed.ToolTip = "Store the movie inside this Movie Player component, embedded in the current scene or prefab.";

			menu.AddOption( "New Movie Resource", resourceIcon, parent.SaveFileAs );

			menu.OpenAtCursor();
		} );

		var sourceGroup = ToolBar.AddGroup( true );

		{
			PlayerDropdown = new ComboBox( this )
			{
				ToolTip = $"Selected {nameof( MoviePlayer )} component",
				HorizontalSizeMode = SizeMode.CanGrow | SizeMode.Expand
			};

			sourceGroup.Layout.Add( PlayerDropdown );
		}
	}

	public void UpdatePlayers( Session? session, IReadOnlyList<MoviePlayer> available )
	{
		PlayerDropdown.Clear();

		foreach ( var player in available.OrderBy( x => x.GameObject.Name ) )
		{
			var resourceName = player.Resource switch
			{
				EmbeddedMovieResource => "Embedded",
				MovieResource resource => resource.ResourceName,
				_ => "None"
			};

			PlayerDropdown.AddItem( $"{player.GameObject.Name} ({resourceName})", "movie", () => Editor.Switch( player ), null, player == session?.Player );
		}

		PlayerDropdown.AddItem( "Create New..", "movie_filter", Editor.CreateNew );
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

		var navigateGroup = ToolBar.AddGroup( true );

		navigateGroup.AddAction( "Back", "arrow_back", parent.ExitSequence,
			() => parent.Session?.Parent is not null );

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
