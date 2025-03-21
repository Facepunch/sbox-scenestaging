﻿using Sandbox.MovieMaker;

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
	private bool _objectSnap;
	private MovieTime _timeOffset;
	private float _pixelsPerSecond;

	public bool IsEditorScene => Player?.Scene?.IsEditor ?? true;

	public bool FrameSnap
	{
		get => _frameSnap;
		set => _frameSnap = Cookies.FrameSnap = value;
	}

	public bool ObjectSnap
	{
		get => _objectSnap;
		set => _objectSnap = Cookies.ObjectSnap = value;
	}

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
		get => EditorData.FrameRate ?? 10;
		set => EditorData = EditorData with { FrameRate = value };
	}

	public int DefaultSampleRate => Clip!.DefaultSampleRate;

	/// <summary>
	/// Current time being edited. In play mode, this is the current playback time.
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

		IsRecording = false;

		EditMode?.Disable();

		Editor.Toolbar.EditModeControls.Clear( true );

		EditMode = type?.Create();
		EditMode?.Enable( this );

		if ( type is not null )
		{
			Cookies.EditMode = type;
		}
	}

	public MovieTime ScenePositionToTime( Vector2 scenePos, SnapFlag ignore = 0, params MovieTime[] snapOffsets ) 
	{
		var time = PixelsToTime( scenePos.x );
		var snapHelper = new TimeSnapHelper( time, PixelsToTime( 16f ), ignore );

		GetSnapTimes( ref snapHelper );

		foreach ( var offset in snapOffsets )
		{
			var offsetHelper = new TimeSnapHelper( time + offset, snapHelper.MaxSnap, snapHelper.Ignore );

			GetSnapTimes( ref offsetHelper );

			snapHelper.Add( offsetHelper );
		}

		return MovieTime.Max( snapHelper.BestTime, MovieTime.Zero );
	}

	private void GetSnapTimes( ref TimeSnapHelper snapHelper )
	{
		if ( FrameSnap )
		{
			var oneFrame = MovieTime.FromFrames( 1, FrameRate );

			snapHelper.Add( SnapFlag.Frame, snapHelper.Time.SnapToGrid( oneFrame ), -3, force: true );
		}

		if ( ObjectSnap )
		{
			snapHelper.Add( SnapFlag.PlayHead, CurrentPointer );

			EditMode?.GetSnapTimes( ref snapHelper );
			Editor.TrackList.DopeSheet.GetSnapTimes( ref snapHelper );
		}
	}

	public MovieTime PixelsToTime( float pixels )
	{
		return MovieTime.FromSeconds( pixels / PixelsPerSecond );
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

	public event Action<MovieTime>? PointerChanged;
	public event Action<MovieTime?>? PreviewChanged;

	public void SetCurrentPointer( MovieTime time )
	{
		CurrentPointer = MovieTime.Max( time, MovieTime.Zero );
		PointerChanged?.Invoke( CurrentPointer );

		if ( IsEditorScene )
		{
			ApplyFrame( CurrentPointer );
		}
		else
		{
			_applyNextFrame = false;
			_lastPlayerPosition = null;

			Player.Position = CurrentPointer;
		}
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

	public bool Frame()
	{
		if ( Clip is not { } clip ) return false;

		PlaybackFrame( clip );

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

		EditMode?.Frame();

		if ( _applyNextFrame )
		{
			ApplyFrame( PreviewPointer ?? CurrentPointer );
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
