using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

/// <summary>
/// Holds and applies pending changes for a track.
/// </summary>
public interface ITrackModificationPreview
{
	IProjectPropertyTrack Track { get; }

	ITrackModification? Modification { get; set; }

	void Clear();
	bool Update( TimeSelection selection, IModificationOptions options );
	bool Commit( TimeSelection selection, IModificationOptions options );
}

internal sealed class TrackModificationPreview<T> : ITrackModificationPreview
{
	public EditMode EditMode { get; }
	public ProjectPropertyTrack<T> Track { get; }
	public ITrackView View { get; }

	IProjectPropertyTrack ITrackModificationPreview.Track => Track;

	private readonly List<PropertyBlock<T>> _original = new();
	private readonly List<PropertyBlock<T>> _applied = new();

	private MovieTimeRange? _lastSliceRange;

	public ITrackModification<T>? Modification { get; set; }

	ITrackModification? ITrackModificationPreview.Modification
	{
		get => Modification;
		set
		{
			Modification = (ITrackModification<T>?)value;

			_original.Clear();
			_lastSliceRange = null;
		}
	}

	public TrackModificationPreview( EditMode editMode, ProjectPropertyTrack<T> track )
	{
		EditMode = editMode;
		Track = track;
		View = EditMode.Session.TrackList.Find( track )
			?? throw new Exception( "Can't find view for track!" );
	}

	public void Clear()
	{
		EditMode.ClearPreviewBlocks( Track );
	}

	public bool Update( TimeSelection selection, IModificationOptions options )
	{
		if ( Modification is not { } modification || View.IsLocked )
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

	public bool Commit( TimeSelection selection, IModificationOptions options )
	{
		if ( !Update( selection, options ) || _applied is not { } blended ) return false;

		var changed = Track.AddRange( blended );

		Clear();

		return changed;
	}
}
