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
	private ComboBox ClipDropDown { get; }

	public ListPanel( MovieEditor parent, Session session )
		: base( parent )
	{
		TrackList = new TrackListWidget( this, session );

		Layout.Add( TrackList );

		MinimumWidth = 250;

		var sourceGroup = ToolBar.AddGroup( true );

		{
			PlayerDropdown = new ComboBox( this );
			PlayerDropdown.ToolTip = $"Selected {nameof( MoviePlayer )} component";

			sourceGroup.Layout.Add( PlayerDropdown );
		}

		{
			ClipDropDown = new ComboBox( this );
			ClipDropDown.ToolTip = $"Selected {nameof( MovieClip )}";

			sourceGroup.Layout.Add( ClipDropDown );
		}
	}

	public void UpdatePlayers( Session? session, IReadOnlyList<MoviePlayer> available )
	{
		PlayerDropdown.Clear();

		foreach ( var player in available.OrderBy( x => x.GameObject.Name ) )
		{
			PlayerDropdown.AddItem( $"{player.GameObject.Name}", "movie", () => Editor.Switch( player ), null, player == session?.Player );
		}

		PlayerDropdown.AddItem( "Create New..", "movie_filter", () => Editor.CreateNew() );
	}

	public void UpdateSources( Session? session )
	{
		ClipDropDown.Clear();

		ClipDropDown.AddItem( "Embedded", "attachment", Editor.SwitchToEmbedded,
			"Use a clip stored in the player component.", session?.Resource is EmbeddedMovieResource );

		var icon = typeof( MovieResource ).GetCustomAttribute<GameResourceAttribute>()!.Icon;

		foreach ( var resource in ResourceLibrary.GetAll<MovieResource>().OrderBy( x => x.ResourcePath ) )
		{
			ClipDropDown.AddItem( resource.ResourceName, icon, () => Editor.SwitchResource( resource ),
				resource.ResourcePath, session?.Resource == resource );
		}

		ClipDropDown.AddItem( "Save As..", "save_as", Editor.SaveFileAs, "Save the current clip as a new movie file." );
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

		var fileGroup = ToolBar.AddGroup( true );

		fileGroup.AddAction( "Save", "save", parent.OnSave, () => parent.Session?.HasUnsavedChanges ?? false );

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
