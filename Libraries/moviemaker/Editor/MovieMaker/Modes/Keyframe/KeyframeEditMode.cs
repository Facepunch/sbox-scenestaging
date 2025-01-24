using Sandbox.MovieMaker;
using System.Linq;

namespace Editor.MovieMaker;

#nullable enable

[Title( "Keyframe Editor" ), Icon( "timeline" ), Order( 0 )]
internal sealed class KeyframeEditMode : EditMode
{
	private IEnumerable<KeyframeHandle> SelectedHandles => SelectedItems.OfType<KeyframeHandle>();
	private IEnumerable<TrackKeyframes> SelectedTracks => SelectedHandles.Select( x => x.Keyframes ).Distinct();

	private readonly Dictionary<DopeSheetTrack, TrackKeyframes> _keyframeMap = new();

	/// <summary>
	/// If true, we automatically record new keyframes when properties are changed
	/// </summary>
	public bool KeyframeRecording { get; set; }
	public InterpolationMode DefaultInterpolation { get; private set; } = InterpolationMode.QuadraticInOut;

	public override bool AllowTrackCreation => KeyframeRecording;

	private TrackKeyframes? GetKeyframes( TrackWidget? track )
	{
		return track is not null ? GetKeyframes( track.DopeSheetTrack ) : null;
	}

	private TrackKeyframes? GetKeyframes( DopeSheetTrack? track )
	{
		return track is not null ? _keyframeMap.GetValueOrDefault( track ) : null;
	}

	private void WriteTracks( IEnumerable<TrackKeyframes> tracks )
	{
		foreach ( var track in tracks )
		{
			track.Write();
		}
	}

	protected override void OnEnable()
	{
		var btn = Toolbar.AddToggle( "Create Keyframes on Edit", "radio_button_checked",
			() => KeyframeRecording,
			x => KeyframeRecording = x );

		btn.ForegroundActive = Theme.Red;

		Toolbar.AddSpacingCell();

		foreach ( var interpolation in Enum.GetValues<InterpolationMode>() )
		{
			Toolbar.AddToggle( interpolation,
				() => SelectedHandles.Any()
					? SelectedHandles.All( x => x.Interpolation == interpolation )
					: DefaultInterpolation == interpolation,
				_ => SetInterpolation( interpolation ) );
		}
	}

	protected override void OnTrackAdded( DopeSheetTrack track )
	{
		var keyframes = new TrackKeyframes( track, this );

		_keyframeMap[track] = keyframes;

		keyframes.Read();
	}

	protected override void OnTrackRemoved( DopeSheetTrack track )
	{
		if ( GetKeyframes( track ) is { } keyframes )
		{
			keyframes.Dispose();

			_keyframeMap.Remove( track );
		}
	}

	[Shortcut( "keyframe-edit.interp-none", "0" )]
	public void SetInterpolationNone() => SetInterpolation( InterpolationMode.None );

	[Shortcut( "keyframe-edit.interp-linear", "1" )]
	public void SetInterpolationLinear() => SetInterpolation( InterpolationMode.Linear );

	[Shortcut( "keyframe-edit.interp-in", "2" )]
	public void SetInterpolationIn() => SetInterpolation( InterpolationMode.QuadraticIn );

	[Shortcut( "keyframe-edit.interp-out", "3" )]
	public void SetInterpolationOut() => SetInterpolation( InterpolationMode.QuadraticOut );

	[Shortcut( "keyframe-edit.interp-in-out", "4" )]
	public void SetInterpolationInOut() => SetInterpolation( InterpolationMode.QuadraticInOut );

	public void SetInterpolation( InterpolationMode value )
	{
		DefaultInterpolation = value;

		foreach ( var h in SelectedHandles )
		{
			h.Interpolation = value;
		}

		WriteTracks( SelectedTracks );
	}

	[Shortcut( "keyframe-edit.nudge-left", "LEFT" )]
	public void NudgeLeft()
	{
		Nudge((Application.KeyboardModifiers & KeyboardModifiers.Shift) != 0 ? -1.0f : -0.1f );
	}

	[Shortcut( "keyframe-edit.nudge-right", "RIGHT" )]
	public void NudgeRight()
	{
		Nudge( (Application.KeyboardModifiers & KeyboardModifiers.Shift) != 0 ? 1.0f : 0.1f );
	}

	private void Nudge( float amount )
	{
		foreach ( var h in SelectedHandles )
		{
			h.Nudge( amount );
		}

		WriteTracks( SelectedTracks );
	}

	private record struct CopiedHandle( Guid Track, float Time, object? Value, InterpolationMode Interpolation );
	private static List<CopiedHandle>? Copied { get; set; }

	protected override void OnCopy()
	{
		Copied = new();

		foreach ( var handle in SelectedHandles )
		{
			Copied.Add( new CopiedHandle( handle.Track.TrackWidget.MovieTrack.Id, handle.Time, handle.Value, handle.Interpolation ) );
		}
	}

	protected override void OnPaste()
	{
		if ( Copied is not { Count: > 0 } copied )
			return;

		var pastePointer = Session.CurrentPointer;
		pastePointer -= copied.Min( x => x.Time );

		var modified = new HashSet<TrackKeyframes>();

		foreach ( var entry in Copied )
		{
			var track = TrackList.Tracks.FirstOrDefault( x => x.MovieTrack.Id == entry.Track );
			if ( track is null || GetKeyframes( track ) is not { } keyframes ) continue;

			keyframes.AddKey( entry.Time + pastePointer, entry.Value, entry.Interpolation );

			modified.Add( keyframes );
		}

		WriteTracks( modified );
	}

	protected override void OnDelete()
	{
		var modifiedTracks = SelectedHandles.Select( x => x.Keyframes )
			.Distinct()
			.ToArray();

		foreach ( var h in SelectedHandles )
		{
			h.Destroy();
		}

		WriteTracks( modifiedTracks );
	}

	protected override void OnTrackLayout( DopeSheetTrack track, Rect rect )
	{
		GetKeyframes( track )?.PositionHandles();
	}

	protected override bool OnPreChange( DopeSheetTrack track )
	{
		if ( !KeyframeRecording )
		{
			return false;
		}

		if ( GetKeyframes( track ) is not { } keyframes )
		{
			return false;
		}

		// When about to change a track that doesn't have any keyframes, make a keyframe at t=0
		// with the old value.

		var movieTrack = track.TrackWidget.MovieTrack;

		if ( movieTrack.Blocks.Count > 0 )
		{
			return false;
		}

		keyframes.AddKey( 0f );
		keyframes.Write();

		return true;
	}

	protected override bool OnPostChange( DopeSheetTrack track )
	{
		if ( GetKeyframes( track ) is not { } keyframes )
		{
			return false;
		}

		if ( KeyframeRecording )
		{
			keyframes.AddKey( Session.CurrentPointer );
		}
		else if ( !keyframes.UpdateKey( Session.CurrentPointer ) )
		{
			return false;
		}

		keyframes.Write();

		return true;
	}
}

internal sealed class TrackKeyframes : IDisposable
{
	private static Dictionary<Type, Color> HandleColors { get; } = new()
	{
		{ typeof(Vector3), Theme.Blue },
		{ typeof(Rotation), Theme.Green },
		{ typeof(Color), Theme.Pink },
		{ typeof(float), Theme.Yellow },
	};

	public KeyframeCurve? Curve { get; private set; }

	public DopeSheetTrack DopeSheetTrack { get; }
	public TrackWidget TrackWidget { get; }
	public KeyframeEditMode EditMode { get; }

	public Color HandleColor { get; private set; }

	public TrackKeyframes( DopeSheetTrack track, KeyframeEditMode editMode )
	{
		DopeSheetTrack = track;
		TrackWidget = track.TrackWidget;
		EditMode = editMode;

		HandleColor = Theme.Grey;

		if ( HandleColors.TryGetValue( track.TrackWidget.MovieTrack.PropertyType, out var color ) )
		{
			HandleColor = color;
		}
	}

	private IEnumerable<KeyframeHandle> Handles => DopeSheetTrack.Children.OfType<KeyframeHandle>();

	public void PositionHandles()
	{
		foreach ( var handle in Handles )
		{
			handle.UpdatePosition();
		}

		DopeSheetTrack.Update();
	}

	internal void AddKey( float time ) => AddKey( time, TrackWidget.Property!.Value );

	internal bool UpdateKey( float time ) => UpdateKey( time, TrackWidget.Property!.Value );

	internal void AddKey( float time, object? value, InterpolationMode? interpolation = null )
	{
		var h = FindKey( time ) ?? new KeyframeHandle( this ) { Interpolation = EditMode.DefaultInterpolation };

		UpdateKey( h, time, value, interpolation );
	}

	internal bool UpdateKey( float time, object? value, InterpolationMode? interpolation = null )
	{
		if ( FindKey( time ) is not { } h ) return false;

		UpdateKey( h, time, value, interpolation );

		return true;
	}

	private void UpdateKey( KeyframeHandle h, float time, object? value, InterpolationMode? interpolation )
	{
		//EditorUtility.PlayRawSound( "sounds/editor/add.wav" );
		h.Time = time;
		h.Value = value;
		h.Interpolation = interpolation ?? h.Interpolation;

		h.UpdatePosition();

		DopeSheetTrack.Update();
	}

	private KeyframeHandle? FindKey( float time ) => Handles.FirstOrDefault( x => x.Time.AlmostEqual( time, 0.001f ) );

	/// <summary>
	/// Read from the Clip
	/// </summary>
	public void Read()
	{
		foreach ( var h in Handles )
		{
			h.Destroy();
		}

		if ( TrackWidget.Property?.CanHaveKeyframes() ?? false )
		{
			Curve = TrackWidget.MovieTrack.ReadKeyframes() ?? KeyframeCurve.Create( TrackWidget.MovieTrack.PropertyType );

			foreach ( var keyframe in Curve )
			{
				_ = new KeyframeHandle( this )
				{
					Time = keyframe.Time,
					Value = keyframe.Value,
					Interpolation = keyframe.Interpolation ?? Curve.Interpolation
				};
			}
		}

		PositionHandles();
	}

	/// <summary>
	/// Write from this sheet to the target
	/// </summary>
	public void Write()
	{
		if ( Curve is null ) return;

		Curve.Clear();

		foreach ( var handle in Handles )
		{
			Curve.SetKeyframe( handle.Time, handle.Value, handle.Interpolation );
		}

		TrackWidget.MovieTrack.WriteKeyframes( Curve );

		Session.Current?.ClipModified();

		DopeSheetTrack.UpdateBlockPreviews();
	}

	public void Dispose()
	{
		foreach ( var handle in Handles )
		{
			handle.Destroy();
		}
	}
}
