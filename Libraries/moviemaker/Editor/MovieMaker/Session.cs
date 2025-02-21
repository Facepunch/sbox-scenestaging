using System.Linq;
using Sandbox;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

/// <summary>
/// Centralizes the current state of a moviemaker editor session
/// </summary>
public sealed partial class Session
{
	public static Session? Current { get; internal set; }

	public MoviePlayer Player { get; private set; } = null!;

	internal MovieClip? Clip { get; private set; }
	internal MovieEditor Editor { get; set; } = null!;

	private bool _frameSnap;
	private bool _blockSnap;
	private MovieTime _timeOffset;
	private float _pixelsPerSecond;

	public bool Playing { get; set; }

	public bool FrameSnap
	{
		get => _frameSnap;
		set => _frameSnap = Cookies.FrameSnap = value;
	}

	public bool BlockSnap
	{
		get => _blockSnap;
		set => _blockSnap = Cookies.BlockSnap = value;
	}

	public bool Loop { get; set; } = true;

	public MovieTime TimeOffset
	{
		get => _timeOffset;
		private set => _timeOffset = Cookies.TimeOffset = value;
	}

	public float PixelsPerSecond
	{
		get => _pixelsPerSecond;
		private set => _pixelsPerSecond = Cookies.PixelsPerSecond = value;
	}

	public MovieClipEditorData EditorData
	{
		get => Clip?.ReadEditorData() ?? new MovieClipEditorData();
		set => Clip?.WriteEditorData( value );
	}

	public int FrameRate
	{
		get => EditorData.FrameRate ?? 30;
		set => EditorData = EditorData with { FrameRate = value };
	}

	public int DefaultSampleRate => Clip!.DefaultSampleRate;

	/// <summary>
	/// When editing keyframes, what time are we changing.
	/// </summary>
	public MovieTime CurrentPointer { get; private set; }

	/// <summary>
	/// What time are we previewing (when holding shift and moving mouse over timeline).
	/// </summary>
	public MovieTime? PreviewPointer { get; private set; }

	public bool HasUnsavedChanges { get; private set; }

	public EditMode? EditMode { get; private set; }

	SmoothDeltaFloat SmoothZoom = new SmoothDeltaFloat { Value = 100.0f, Target = 100.0f, SmoothTime = 0.3f };
	SmoothDeltaFloat SmoothPan = new SmoothDeltaFloat { Value = 0.0f, Target = 0f, SmoothTime = 0.3f };

	public MovieTimeRange VisibleTimeRange
	{
		get
		{
			var minTime = PixelsToTime( 0f ) + TimeOffset;
			var maxTime = PixelsToTime( Editor.TrackList.RightWidget.Width ) + TimeOffset;

			return new (minTime, maxTime);
		}
	}

	private MovieTime? _lastPlayerPosition;

	/// <summary>
	/// Invoked when the view pans or changes scale.
	/// </summary>
	public event Action? ViewChanged;

	internal void SetPlayer( MoviePlayer player )
	{
		Player = player;
		Clip = player.MovieClip;
	}

	internal void SetEditMode( EditModeType? type )
	{
		if ( type?.IsMatchingType( EditMode ) ?? EditMode is null ) return;

		EditMode?.Disable();

		Editor.Toolbar.EditModeControls.Clear( true );

		EditMode = type?.Create();
		EditMode?.Enable( this );

		if ( type is not null )
		{
			Cookies.EditMode = type;
		}
	}

	public MovieTime ScenePositionToTime( Vector2 scenePos, float height = 0f, params MovieTime[] snapOffsets ) 
	{
		var time = PixelsToTime( scenePos.x );
		var snapHelper = new TimeSnapHelper( time, PixelsToTime( 8f ) );

		GetSnapTimes( ref snapHelper, scenePos, height, true );

		foreach ( var offset in snapOffsets )
		{
			var offsetHelper = new TimeSnapHelper( time + offset, snapHelper.MaxSnap );

			GetSnapTimes( ref offsetHelper, scenePos + new Vector2( TimeToPixels( offset ), 0f ), height, true );

			snapHelper.Add( offsetHelper );
		}

		return snapHelper.BestTime;
	}

	public MovieTime ScenePositionToTimeIgnorePointer( Vector2 scenePos )
	{
		var time = PixelsToTime( scenePos.x );
		var snapHelper = new TimeSnapHelper( time, PixelsToTime( 8f ) );

		GetSnapTimes( ref snapHelper, scenePos, 0f, false );

		return snapHelper.BestTime;
	}

	private void GetSnapTimes( ref TimeSnapHelper snapHelper, Vector2 scenePos, float height, bool includePointer )
	{
		if ( includePointer )
		{
			snapHelper.Add( CurrentPointer );
		}

		if ( FrameSnap )
		{
			var oneFrame = MovieTime.FromFrames( 1, FrameRate );

			snapHelper.MaxSnap = MovieTime.Max( snapHelper.MaxSnap, oneFrame * 2 );
			snapHelper.Add( PixelsToTime( scenePos.x ).SnapToGrid( oneFrame ), -1 );
		}

		if ( BlockSnap )
		{
			EditMode?.GetSnapTimes( ref snapHelper, scenePos, height );
			Editor.TrackList.DopeSheet.GetSnapTimes( ref snapHelper, scenePos, height );
		}
	}

	public MovieTime PixelsToTime( float pixels, bool snap = false )
	{
		var t = MovieTime.FromSeconds( pixels / PixelsPerSecond );

		if ( snap )
		{
			t = t.SnapToGrid( MinorTick.Interval );
		}

		return t;
	}

	public float TimeToPixels( MovieTime time )
	{
		return (float)(time.TotalSeconds * PixelsPerSecond);
	}

	public void ScrollBy( float x, bool smooth )
	{
		if ( x == 0 )
			return;

		var time = PixelsToTime( x );

		SmoothPan.Target -= (float)time.TotalSeconds;
		if ( SmoothPan.Target < 0 ) SmoothPan.Target = 0;

		if ( !smooth )
		{
			SmoothPan.Value = SmoothPan.Target;
			SmoothPan.Velocity = 0;
			TimeOffset = MovieTime.FromSeconds( SmoothPan.Target );
		}

		ViewChanged?.Invoke();
	}

	public void ApplyFrame( MovieTime time )
	{
		if ( EditMode is null )
		{
			Player.ApplyFrame( time );
		}
		else
		{
			EditMode?.ApplyFrame( time );
		}
	}

	public event Action<MovieTime>? PointerChanged;
	public event Action<MovieTime?>? PreviewChanged;

	public void SetCurrentPointer( MovieTime time )
	{
		CurrentPointer = MovieTime.Max( time, MovieTime.Zero );
		PointerChanged?.Invoke( CurrentPointer );

		ApplyFrame( CurrentPointer );
	}

	public void SetPreviewPointer( MovieTime time )
	{
		PreviewPointer = MovieTime.Max( time, MovieTime.Zero );
		PreviewChanged?.Invoke( PreviewPointer );

		ApplyFrame( PreviewPointer.Value );
	}

	public void ClearPreviewPointer()
	{
		PreviewPointer = null;

		PreviewChanged?.Invoke( PreviewPointer );

		ApplyFrame( CurrentPointer );
	}

	public void Play()
	{
		if ( Playing ) return;

		Playing = true;
	}

	public void Stop()
	{
		if ( !Playing ) return;

		Playing = false;
	}

	public bool Frame()
	{
		if ( !Playing && _lastPlayerPosition is { } lastPlayerPosition && lastPlayerPosition != Player.Position )
		{
			CurrentPointer = lastPlayerPosition;
			PointerChanged?.Invoke( CurrentPointer );
		}

		_lastPlayerPosition = Player.Position;

		if ( Playing )
		{
			var targetTime = CurrentPointer + MovieTime.FromSeconds( RealTime.Delta );

			// got to the end
			if ( targetTime >= Clip.Duration && Clip.Duration > MovieTime.Zero )
			{
				if ( Loop )
				{
					targetTime = MovieTime.Zero;
				}
				else
				{
					targetTime = Clip.Duration;

					Playing = false;
				}
			}

			SetCurrentPointer( targetTime );
		}

		if ( SmoothZoom.Update( RealTime.Delta ) )
		{
			var d = TimeToPixels( TimeOffset ) - TimeToPixels( CurrentPointer );

			PixelsPerSecond = SmoothZoom.Value;
			PixelsPerSecond = PixelsPerSecond.Clamp( 5, 1024 );

			var nd = TimeToPixels( TimeOffset ) - TimeToPixels( CurrentPointer );
			ScrollBy( nd - d, false );

			ViewChanged?.Invoke();
		}

		if ( SmoothPan.Update( RealTime.Delta ) )
		{
			TimeOffset = MovieTime.FromSeconds( SmoothPan.Value );
			ViewChanged?.Invoke();
		}

		return true;
	}

	internal void Zoom( float v )
	{
		SmoothZoom.Target = SmoothZoom.Target += (v * SmoothZoom.Target) * 0.01f;
		SmoothZoom.Target = SmoothZoom.Target.Clamp( 5, 1024 );
	}

	internal void ClipModified()
	{
		if ( Clip == Player.EmbeddedClip )
		{
			Player.Scene.Editor.HasUnsavedChanges = true;
			return;
		}

		HasUnsavedChanges = true;
	}

	public void Save()
	{
		HasUnsavedChanges = false;

		// If we're embedded, save the scene

		if ( Clip == Player.EmbeddedClip )
		{
			Player.Scene.Editor.Save( false );
			return;
		}

		// If we're referencing a .movie resource, save it to disk

		if ( Clip != Player.ReferencedClip?.Clip )
		{
			return;
		}

		if ( AssetSystem.FindByPath( Player.ReferencedClip!.ResourcePath ) is { } asset )
		{
			asset.SaveToDisk( Player.ReferencedClip );
		}
	}

	public void DispatchViewChanged()
	{
		ViewChanged?.Invoke();
		EditMode?.ViewChanged( Editor.TrackList.DopeSheet.VisibleRect );
	}

	public void TrackModified( MovieTrack track )
	{
		ClipModified();

		Editor.TrackList.FindTrack( track )?.DopeSheetTrack?.UpdateBlockItems();
	}
}
