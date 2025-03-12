using System.Linq;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

/// <summary>
/// Holds and applies pending changes for a track.
/// </summary>
internal interface ITrackModification
{
	ProjectPropertyTrack Track { get; }

	void SetRelativeTo( object? value );
	void SetChanges( object? constantValue );
	void SetChanges( IEnumerable<PropertyBlock> blocks );
	void ClearPreview();
	bool Update( TimeSelection selection, MovieTime offset, bool additive );
	bool Commit( TimeSelection selection, MovieTime offset, bool additive );
}

internal sealed class TrackModification<T> : ITrackModification
{
	public EditMode EditMode { get; }
	public ProjectPropertyTrack<T> Track { get; }

	ProjectPropertyTrack ITrackModification.Track => Track;

	private PropertyBlock<T>? _original;
	private PropertyBlock<T>? _overlay;
	private PropertyBlock<T>? _blended;

	private T _relativeTo = default!;

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

	public void SetChanges( object? constantValue ) => SetChanges(
		[new ConstantPropertyBlock<T>( (MovieTime.Zero, MovieTime.MaxValue), (T)constantValue! )] );

	public void SetChanges( IEnumerable<PropertyBlock> blocks )
	{
		_overlay = blocks.Cast<PropertyBlock<T>>().Stitch();
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

		if ( _original is null || _original.TimeRange != timeRange )
		{
			var slice = Track.Slice( timeRange );

			if ( slice.Count == 0 )
			{
				slice =
				[
					Track.Blocks.Count == 0
						? new ConstantPropertyBlock<T>( timeRange, _relativeTo )
						: Track.Blocks.GetLastBlock( selection.TotalStart )
				];
			}

			_original = slice.Stitch().Slice( timeRange );
		}

		_blended = new PropertyBlockBlend<T>( _original, _overlay.Shift( offset ), selection );

		EditMode.SetPreviewBlocks( Track, [_blended] );

		return true;
	}

	public bool Commit( TimeSelection selection, MovieTime offset, bool additive )
	{
		if ( !Update( selection, offset, additive ) || _blended is not { } blended ) return false;

		ClearPreview();

		return Track.Add( blended );
	}
}
