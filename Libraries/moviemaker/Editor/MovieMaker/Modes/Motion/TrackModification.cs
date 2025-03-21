using System.Collections.Immutable;
using System.Linq;
using System.Threading.Channels;
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

	void SetRelativeTo( object? constantValue );
	void SetConstantOverlay( object? constantValue );
	void SetClipboardOverlay( IEnumerable<IProjectPropertyBlock> blocks );
	void ClearPreview();
	bool Update( ModificationOptions options );
	bool Commit( ModificationOptions options );

	TrackModificationSnapshot Snapshot();
	void Restore( TrackModificationSnapshot state );
}

internal record TrackModificationSnapshot( ITrackOverlay? Overlay );

internal interface ITrackOverlay;

internal interface ITrackOverlay<T> : ITrackOverlay
{
	IEnumerable<PropertyBlock<T>> Blend( IReadOnlyList<PropertyBlock<T>> original, ModificationOptions options );

	protected static PropertyBlock<T> Blend( PropertySignal<T>? original, PropertySignal<T>? overlay, MovieTimeRange timeRange, ModificationOptions options )
	{
		overlay = overlay?.Shift( options.Offset );

		if ( original is null && overlay is null )
		{
			throw new ArgumentNullException( nameof(overlay), "Expected at least one signal." );
		}

		if ( original is null || overlay is null )
		{
			return new PropertyBlock<T>( (original ?? overlay)!.Reduce( timeRange ), timeRange );
		}

		return new PropertyBlock<T>( original.CrossFade( overlay.Shift( options.Offset ), options.Selection ).Reduce( timeRange ), timeRange );
	}
}

file sealed record SignalOverlay<T>( PropertySignal<T> Original, PropertySignal<T> Changed ) : ITrackOverlay<T>
{
	public IEnumerable<PropertyBlock<T>> Blend( IReadOnlyList<PropertyBlock<T>> original, ModificationOptions options )
	{
		if ( options.Additive ) throw new NotImplementedException();

		var timeRange = options.Selection.TotalTimeRange;

		// Fill in gaps between blocks in original track with AsSignal()

		if ( original.AsSignal() is not { } originalSignal )
		{
			yield return new PropertyBlock<T>( Changed, timeRange );
			yield break;
		}

		yield return ITrackOverlay<T>.Blend( originalSignal, Changed, timeRange, options );
	}
}

file sealed record ClipboardOverlay<T>( ImmutableArray<PropertyBlock<T>> Blocks ) : ITrackOverlay<T>
{
	public IEnumerable<PropertyBlock<T>> Blend( IReadOnlyList<PropertyBlock<T>> original, ModificationOptions options )
	{
		if ( options.Additive ) throw new NotImplementedException();

		var timeRanges = original.Select( x => x.TimeRange )
			.Union( Blocks.Select( x => x.TimeRange + options.Offset ) );

		foreach ( var timeRange in timeRanges )
		{
			var originalSignal = original
				.Where( x => timeRange.Contains( x.TimeRange ) )
				.AsSignal();

			var overlaySignal = Blocks
				.Where( x => timeRange.Contains( x.TimeRange + options.Offset ) )
				.AsSignal();

			yield return ITrackOverlay<T>.Blend( originalSignal, overlaySignal, timeRange, options );
		}
	}
}

internal sealed class TrackModification<T> : ITrackModification
{
	public EditMode EditMode { get; }
	public ProjectPropertyTrack<T> Track { get; }

	IProjectPropertyTrack ITrackModification.Track => Track;

	private readonly List<PropertyBlock<T>> _original = new();
	private ITrackOverlay<T>? _overlay;

	private readonly List<PropertyBlock<T>> _blended = new();

	private MovieTimeRange? _lastSliceRange;

	public bool HasChanges => _overlay is not null;

	public TrackModification( EditMode editMode, ProjectPropertyTrack<T> track )
	{
		EditMode = editMode;
		Track = track;
	}

	public void SetRelativeTo( object? constantValue )
	{
		PropertySignal<T> constantSignal = (T)constantValue!;

		_overlay = _overlay is not SignalOverlay<T> signalOverlay
			? new SignalOverlay<T>( constantSignal, constantSignal )
			: signalOverlay with { Original = constantSignal };
	}

	public void SetConstantOverlay( object? constantValue )
	{
		PropertySignal<T> constantSignal = (T)constantValue!;

		_overlay = _overlay is not SignalOverlay<T> signalOverlay
			? new SignalOverlay<T>( constantSignal, constantSignal )
			: signalOverlay with { Changed = constantSignal };
	}

	public void SetClipboardOverlay( IEnumerable<IProjectPropertyBlock> blocks )
	{
		var overlay = new ClipboardOverlay<T>( [.. blocks.Cast<PropertyBlock<T>>()] );

		_overlay = overlay.Blocks.Length > 0
			? overlay
			: null;
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

		if ( _lastSliceRange != timeRange )
		{
			_lastSliceRange = timeRange;

			_original.Clear();
			_original.AddRange( Track.Slice( timeRange ) );
		}

		_blended.Clear();
		_blended.AddRange( _overlay.Blend( _original, options ) );

		EditMode.SetPreviewBlocks( Track, _blended );

		return true;
	}

	public bool Commit( ModificationOptions options )
	{
		if ( !Update( options ) || _blended is not { } blended ) return false;

		var changed = Track.AddRange( blended );

		ClearPreview();

		return changed;
	}

	public TrackModificationSnapshot Snapshot() => new ( _overlay );

	public void Restore( TrackModificationSnapshot state )
	{
		_overlay = (ITrackOverlay<T>?)state.Overlay;

		_original.Clear();
		_lastSliceRange = null;
	}
}
