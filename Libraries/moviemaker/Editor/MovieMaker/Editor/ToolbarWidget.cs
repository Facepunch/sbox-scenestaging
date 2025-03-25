using System.Linq;
using System.Reflection;
using Sandbox.MovieMaker;
using Sandbox.MovieMaker.Compiled;
using Sandbox.UI;

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
		Layout.Spacing = 4f;
		Layout.Margin = 4f;

		var targetControls = new ControlGroup();

		Layout.Add( targetControls );

		{
			PlayerDropdown = new ComboBox( this );
			PlayerDropdown.MinimumWidth = 150;
			PlayerDropdown.ToolTip = $"Selected {nameof( MoviePlayer )} component";

			targetControls.Layout.Add( PlayerDropdown );
		}

		{
			ClipDropDown = new ComboBox( this );
			ClipDropDown.MinimumWidth = 150;
			ClipDropDown.ToolTip = $"Selected {nameof( MovieClip )}";

			targetControls.Layout.Add( ClipDropDown );
		}

		var playbackControls = new ControlGroup();

		Layout.Add( playbackControls );

		Sandbox.Bind.Builder AddToggle( string title, string icon, Color? activeColor = null, Layout parent = null )
		{
			var btn = new IconButton( icon );
			btn.ToolTip = title;
			btn.IsToggle = true;
			btn.IconSize = 16;
			btn.Background = Color.Transparent;
			btn.BackgroundActive = Color.Transparent;
			btn.ForegroundActive = activeColor ?? Theme.Primary;

			(parent ?? Layout).Add( btn );

			return btn.Bind( "IsActive" );
		}

		AddToggle( "Toggle Record", "radio_button_checked", activeColor: Theme.Red, parent: playbackControls.Layout )
			.From( () => Session.IsRecording, x => Session.IsRecording = x );

		AddToggle( "Toggle Play", "play_arrow", parent: playbackControls.Layout )
			.From( () => Session.IsPlaying, x => Session.IsPlaying = x );

		AddToggle( "Loop at End of Playback", "repeat", parent: playbackControls.Layout )
			.From( () => Session.IsLooping, x => Session.IsLooping = x );

		{
			var slider = new FloatSlider( null )
			{
				ToolTip = "Playback Rate", FixedWidth = 80f,
				Minimum = 0f, Maximum = 2f,
				Step = 0.1f
			};

			slider.Bind( nameof(FloatSlider.Value) )
				.From( () => Session.TimeScale, value => Session.TimeScale = value );

			playbackControls.Layout.Add( slider );

			var speed = new Label( null )
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

			playbackControls.Layout.Add( speed );
		}

		if ( EditMode.AllTypes.Count > 1 )
		{
			foreach ( var type in EditMode.AllTypes )
			{
				var btn = new IconButton( type.Icon ) { ToolTip = type.Title, IsToggle = true, IconSize = 16 };

				btn.Bind( "IsActive" ).From( () => type.IsMatchingType( Session.EditMode ), x => Session.SetEditMode( type ) );

				Layout.Add( btn );
			}

			Layout.AddSpacingCell( 16f );
		}

		var editModeControls = new ControlGroup();

		EditModeControls = editModeControls.Layout;

		Layout.Add( editModeControls );

		Layout.AddStretchCell();

		var viewControls = new ControlGroup();

		Layout.Add( viewControls );

		{
			AddToggle( "Object Snap", "align_horizontal_left", parent: viewControls.Layout )
				.From( () => Session.ObjectSnap, x => Session.ObjectSnap = x );

			AddToggle( "Frame Snap", "straighten", parent: viewControls.Layout )
				.From( () => Session.FrameSnap, x => Session.FrameSnap = x );

			var rate = new ComboBox();

			foreach ( var frameRate in MovieTime.SupportedFrameRates )
			{
				rate.AddItem( $"{frameRate} FPS", onSelected: () => Session.FrameRate = frameRate,
					selected: Session.FrameRate == frameRate );
			}

			viewControls.Layout.Add( rate );
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

		ClipDropDown.AddItem( "Embedded", "attachment", Editor.SwitchToEmbedded, "Use a clip stored in the player component.", Session?.Resource is EmbeddedMovieResource );

		var icon = typeof(MovieResource).GetCustomAttribute<GameResourceAttribute>()!.Icon;

		foreach ( var resource in ResourceLibrary.GetAll<MovieResource>().OrderBy( x => x.ResourcePath ) )
		{
			ClipDropDown.AddItem( resource.ResourceName, icon, () => Editor.SwitchResource( resource ), resource.ResourcePath, Session?.Resource == resource );
		}

		ClipDropDown.AddItem( "Save As..", "save_as", Editor.SaveFileAs, "Save the current clip as a new movie file." );
	}
}

file sealed class ControlGroup : Widget
{
	public ControlGroup()
	{
		HorizontalSizeMode = SizeMode.CanGrow;

		Layout = Layout.Row();
		Layout.Spacing = 2f;
		Layout.Margin = 4f;
	}

	protected override void OnPaint()
	{
		Paint.ClearPen();
		Paint.SetBrush( Theme.ControlBackground.LerpTo( Theme.WidgetBackground, 0.5f ) );
		Paint.DrawRect( LocalRect, 3f );
	}
}
