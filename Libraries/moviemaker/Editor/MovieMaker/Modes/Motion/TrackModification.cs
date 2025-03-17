using System.Linq;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

/// <summary>
/// Holds and applies pending changes for a track.
/// </summary>
internal interface ITrackModification
{
	IProjectPropertyTrack Track { get; }

	void SetRelativeTo( object? value );
	void SetOverlay( object? constantValue );
	void SetOverlay( IEnumerable<IProjectPropertyBlock> blocks, MovieTime offset );
	void ClearPreview();
	bool Update( TimeSelection selection, MovieTime offset, bool additive );
	bool Commit( TimeSelection selection, MovieTime offset, bool additive );
}

internal sealed class TrackModification<T> : ITrackModification
{
	public EditMode EditMode { get; }
	public ProjectPropertyTrack<T> Track { get; }

	IProjectPropertyTrack ITrackModification.Track => Track;

	private PropertySignal<T>? _original;
	private PropertySignal<T>? _overlay;
	private PropertyBlock<T>? _blended;

	private T _relativeTo = default!;
	private MovieTimeRange? _lastSliceRange;

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

	public void SetOverlay( object? constantValue ) => _overlay = (T)constantValue!;

	public void SetOverlay( IEnumerable<IProjectPropertyBlock> blocks, MovieTime offset )
	{
		_overlay = blocks.Cast<PropertyBlock<T>>().AsSignal()?.Shift( offset );
	}

	public void ClearPreview()
	{
		EditMode.ClearPreviewBlocks( Track );
	}

	public bool Update( TimeSelection selection, MovieTime offset, bool additive )
	{
		if ( _overlay is null || !EditMode.Session.CanEdit( Track ) )
		{
			ClearPreview();
			return false;
		}

		var timeRange = selection.TotalTimeRange;

		if ( _original is null || _lastSliceRange != timeRange )
		{
			_lastSliceRange = timeRange;
			_original = Track.Slice( timeRange ).AsSignal() ?? _relativeTo;
		}

		_blended = new PropertyBlock<T>( _original.CrossFade( _overlay.Shift( offset ), selection ).Reduce( timeRange ), timeRange );

		EditMode.SetPreviewBlocks( Track, [_blended] );

		return true;
	}

	public bool Commit( TimeSelection selection, MovieTime offset, bool additive )
	{
		if ( !Update( selection, offset, additive ) || _blended is not { } blended ) return false;

		var changed = Track.Add( blended.Signal, blended.TimeRange );

		ClearPreview();

		return changed;
	}
}
