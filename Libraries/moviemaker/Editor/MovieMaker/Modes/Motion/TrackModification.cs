using System.Linq;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

public readonly record struct ModificationOptions(
	TimeSelection Selection,
	MovieTime Offset,
	bool Additive,
	int SmoothSteps,
	MovieTime SmoothSize );

/// <summary>
/// Holds and applies pending changes for a track.
/// </summary>
internal interface ITrackModification
{
	IProjectPropertyTrack Track { get; }

	bool HasChanges { get; }

	void SetRelativeTo( object? value );
	void SetOverlay( object? constantValue );
	void SetOverlay( IEnumerable<IProjectPropertyBlock> blocks, MovieTime offset );
	void ClearPreview();
	bool Update( ModificationOptions options );
	bool Commit( ModificationOptions options );

	TrackModificationSnapshot Snapshot();
	void Restore( TrackModificationSnapshot state );
}

internal record TrackModificationSnapshot( IPropertySignal? Overlay, object? RelativeTo );

internal sealed class TrackModification<T> : ITrackModification
{
	public EditMode EditMode { get; }
	public ProjectPropertyTrack<T> Track { get; }

	IProjectPropertyTrack ITrackModification.Track => Track;

	private PropertySignal<T>? _original;
	private PropertySignal<T>? _overlay;
	private PropertySignal<T>? _smoothedOverlay;
	private PropertyBlock<T>? _blended;

	private T _relativeTo = default!;
	private MovieTimeRange? _lastSliceRange;
	private MovieTime _lastSmoothSize;

	public bool HasChanges => _overlay != null;

	public TrackModification( EditMode editMode, ProjectPropertyTrack<T> track )
	{
		EditMode = editMode;
		Track = track;
	}

	public void SetRelativeTo( object? value )
	{
		_relativeTo = (T)value!;
	}

	public void SetOverlay( object? constantValue )
	{
		_overlay = (T)constantValue!;
		_smoothedOverlay = null;
	}

	public void SetOverlay( IEnumerable<IProjectPropertyBlock> blocks, MovieTime offset )
	{
		_overlay = blocks.Cast<PropertyBlock<T>>().AsSignal()?.Shift( offset ).Reduce();
		_smoothedOverlay = null;
	}

	public void ClearPreview()
	{
		EditMode.ClearPreviewBlocks( Track );
	}

	public bool Update( ModificationOptions options )
	{
		if ( _overlay is null || !EditMode.Session.CanEdit( Track ) )
		{
			ClearPreview();
			return false;
		}

		var timeRange = options.Selection.TotalTimeRange;

		if ( _original is null || _lastSliceRange != timeRange )
		{
			_lastSliceRange = timeRange;
			_original = Track.Slice( timeRange ).AsSignal() ?? _relativeTo;
		}

		if ( _smoothedOverlay is null || _lastSmoothSize != options.SmoothSize )
		{
			_lastSmoothSize = options.SmoothSize;
			_smoothedOverlay = _overlay.Smooth( options.SmoothSize );
		}

		_blended = new PropertyBlock<T>( _original.CrossFade( _smoothedOverlay.Shift( options.Offset ), options.Selection ).Reduce( timeRange ), timeRange );

		EditMode.SetPreviewBlocks( Track, [_blended] );

		return true;
	}

	public bool Commit( ModificationOptions options )
	{
		if ( !Update( options ) || _blended is not { } blended ) return false;

		var changed = Track.Add( blended.Signal, blended.TimeRange );

		ClearPreview();

		return changed;
	}

	public TrackModificationSnapshot Snapshot() => new ( _overlay, _relativeTo );

	public void Restore( TrackModificationSnapshot state )
	{
		_overlay = (PropertySignal<T>?)state.Overlay;
		_relativeTo = (T)state.RelativeTo!;

		_original = null;
		_lastSliceRange = null;
		_smoothedOverlay = null;
	}
}
