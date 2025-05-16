using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

/// <summary>
/// Panel containing the timeline.
/// </summary>
public sealed class TimelinePanel : MovieEditorPanel
{
	public Timeline Timeline { get; }

	public TimelinePanel( MovieEditor parent, Session session )
		: base( parent )
	{
		Timeline = new Timeline( session );

		MouseTracking = true;

		Layout.Add( Timeline );

		var playbackGroup = ToolBar.AddGroup( true );

		var recordDisplay = new ToolBarItemDisplay( "Toggle Record", "radio_button_checked",
			"Start or stop recording live changes to tracks in the track list.",
			Background: false );

		playbackGroup.AddToggle( recordDisplay, () => session.IsRecording, x => session.IsRecording = x )
			.ForegroundActive = Theme.Red;

		var playDisplay = new ToolBarItemDisplay( "Toggle Play", "play_arrow",
			"Start or stop playback.",
			Background: false );

		playbackGroup.AddToggle( playDisplay, () => session.IsPlaying, x => session.IsPlaying = x );

		var loopDisplay = new ToolBarItemDisplay( "Toggle Looping", "repeat",
			"When enabled, playback will jump back to the start when reaching the end of the clip.",
			Background: false );

		playbackGroup.AddToggle( loopDisplay, () => session.IsLooping, x => session.IsLooping = x );

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

		var undoGroup = ToolBar.AddGroup( true );

		var undoDisplay = new ToolBarItemDisplay( "Undo", "undo", "Revert the last change made to the movie clip." );
		var redoDisplay = new ToolBarItemDisplay( "Redo", "redo", "Reapply the last undone change made to the movie clip." );

		undoGroup.AddAction( undoDisplay, session.Undo, () => session.History.CanUndo );
		undoGroup.AddAction( redoDisplay, session.Redo, () => session.History.CanRedo );

		if ( EditMode.AllTypes is { Count: > 1 } editModes )
		{
			var editModeGroup = ToolBar.AddGroup( true );

			foreach ( var editModeType in editModes )
			{
				var display = new ToolBarItemDisplay( editModeType.Title, editModeType.Icon, editModeType.Description );

				editModeGroup.AddToggle( display,
					() => editModeType.IsMatchingType( session.EditMode ),
					state => session.SetEditMode( state ? editModeType : null ) );
			}
		}

		var snapGroup = ToolBar.AddGroup( true, true );

		var objectSnapDisplay = new ToolBarItemDisplay( "Object Snap", "align_horizontal_left",
			"Snap to objects in the timeline.",
			Background: false );

		var frameSnapDisplay = new ToolBarItemDisplay( "Frame Snap", "straighten",
			"Snap to the selected frame rate.",
			Background: false );

		snapGroup.AddToggle( objectSnapDisplay, () => session.ObjectSnap, x => session.ObjectSnap = x );

		snapGroup.AddToggle( frameSnapDisplay, () => session.FrameSnap, x => session.FrameSnap = x );

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
