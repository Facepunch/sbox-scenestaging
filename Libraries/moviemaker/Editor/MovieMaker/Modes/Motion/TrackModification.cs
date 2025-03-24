using System.Linq;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

/// <summary>
/// Holds and applies pending changes for a track.
/// </summary>
internal interface ITrackModificationPreview
{
	IProjectPropertyTrack Track { get; }

	ITrackModification? Modification { get; set; }

	void Clear();
	bool Update( TimeSelection selection, ITrackModificationOptions options );
	bool Commit( TimeSelection selection, ITrackModificationOptions options );

	TrackModificationSnapshot Snapshot();
	void Restore( TrackModificationSnapshot state );
}

internal record TrackModificationSnapshot( ITrackModification? Modification );

internal sealed class TrackModificationPreview<T> : ITrackModificationPreview
{
	public EditMode EditMode { get; }
	public ProjectPropertyTrack<T> Track { get; }

	IProjectPropertyTrack ITrackModificationPreview.Track => Track;

	private readonly List<PropertyBlock<T>> _original = new();
	private readonly List<PropertyBlock<T>> _applied = new();

	private MovieTimeRange? _lastSliceRange;

	public ITrackModification<T>? Modification { get; set; }

	ITrackModification? ITrackModificationPreview.Modification
	{
		get => Modification;
		set => Modification = (ITrackModification<T>?)value;
	}

	public TrackModificationPreview( EditMode editMode, ProjectPropertyTrack<T> track )
	{
		EditMode = editMode;
		Track = track;
	}

	public void Clear()
	{
		EditMode.ClearPreviewBlocks( Track );
	}

	public bool Update( TimeSelection selection, ITrackModificationOptions options )
	{
		if ( Modification is not { } modification || !EditMode.Session.CanEdit( Track ) )
		{
			Clear();
			return false;
		}

		var timeRange = selection.TotalTimeRange;

		if ( _lastSliceRange != timeRange )
		{
			_lastSliceRange = timeRange;

			_original.Clear();
			_original.AddRange( Track.Slice( timeRange ) );
		}

		_applied.Clear();
		_applied.AddRange( modification.Apply( _original, selection, options ) );

		EditMode.SetPreviewBlocks( Track, _applied );

		return true;
	}

	public bool Commit( TimeSelection selection, ITrackModificationOptions options )
	{
		if ( !Update( selection, options ) || _applied is not { } blended ) return false;

		var changed = Track.AddRange( blended );

		Clear();

		return changed;
	}

	public TrackModificationSnapshot Snapshot() => new ( Modification );

	public void Restore( TrackModificationSnapshot state )
	{
		Modification = (ITrackModification<T>?)state.Modification;

		_original.Clear();
		_lastSliceRange = null;
	}
}
