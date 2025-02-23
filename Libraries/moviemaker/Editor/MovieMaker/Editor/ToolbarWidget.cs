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

		Sandbox.Bind.Builder AddToggle( string title, string icon, Color? activeColor = null )
		{
			var btn = new IconButton( icon );
			btn.ToolTip = title;
			btn.IsToggle = true;
			btn.IconSize = 16;
			btn.Background = Color.Transparent;
			btn.BackgroundActive = Color.Transparent;
			btn.ForegroundActive = activeColor ?? Theme.Primary;

			Layout.Add( btn );

			return btn.Bind( "IsActive" );
		}

		AddToggle( "Toggle Record", "radio_button_checked", activeColor: Theme.Red )
			.From( () => Session.IsRecording, x => Session.IsRecording = x );

		AddToggle( "Toggle Play", "play_arrow" )
			.From( () => Session.IsPlaying, x => Session.IsPlaying = x );

		AddToggle( "Loop at End of Playback", "repeat" )
			.From( () => Session.IsLooping, x => Session.IsLooping = x );

		{
			var slider = new FloatSlider( this )
			{
				ToolTip = "Playback Rate", FixedWidth = 80f,
				Minimum = 0f, Maximum = 2f,
				Step = 0.1f
			};

			slider.Bind( nameof(FloatSlider.Value) )
				.From( () => Session.TimeScale, value => Session.TimeScale = value );

			Layout.Add( slider );

			var speed = new Label( this )
			{
				Color = Color.White.Darken( 0.5f ),
				FixedWidth = 30f, Margin = 4f,
				Alignment = TextFlag.Center
			};

			speed.Bind( nameof(Label.Text) )
				.ReadOnly()
				.From( () => $"x{Session.TimeScale:F1}", null );

			slider.MouseRightClick += () => Session.TimeScale = 1f;
			speed.MouseRightClick += () => Session.TimeScale = 1f;

			Layout.Add( speed );
		}

		Layout.AddSpacingCell( 16f );

		foreach ( var type in EditMode.AllTypes )
		{
			var btn = new IconButton( type.Icon ) { ToolTip = type.Title, IsToggle = true, IconSize = 16 };

			btn.Bind( "IsActive" ).From( () => type.IsMatchingType( Session.EditMode ), x => Session.SetEditMode( type ) );

			Layout.Add( btn );
		}

		Layout.AddSpacingCell( 16f );

		EditModeControls = Layout.AddRow();
		EditModeControls.Spacing = 2;

		Layout.AddStretchCell();

		{
			AddToggle( "Object Snap", "align_horizontal_left" )
				.From( () => Session.ObjectSnap, x => Session.ObjectSnap = x );

			AddToggle( "Frame Snap", "straighten" )
				.From( () => Session.FrameSnap, x => Session.FrameSnap = x );

			var rate = new ComboBox();

			foreach ( var frameRate in MovieTime.SupportedFrameRates )
			{
				rate.AddItem( $"{frameRate} FPS", onSelected: () => Session.FrameRate = frameRate,
					selected: Session.FrameRate == frameRate );
			}

			Layout.Add( rate );
		}
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

