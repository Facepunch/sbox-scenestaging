using Sandbox.MovieMaker;
using System.Linq;

namespace Editor.MovieMaker;

#nullable enable

/// <summary>
/// Holds and applies pending changes for a track.
/// </summary>
internal interface ITrackModification
{
	MovieTrack Track { get; }

	void SetChanges( MovieTime? originTime, IEnumerable<IMovieBlock> blocks );
	void ClearPreview();
	bool Update( TimeSelection selection, MovieTime offset, bool additive );
	bool Commit( TimeSelection selection, MovieTime offset, bool additive );

	public void SetChanges( MovieTime? originTime, params IMovieBlock[] blocks ) =>
		SetChanges( originTime, blocks.AsEnumerable() );

	public void SetChanges( MovieTime? originTime, object? constantValue ) =>
		SetChanges( originTime, new MovieBlockSlice( (MovieTime.Zero, MovieTime.MaxValue),
			EditHelpers.CreateConstantData( Track.PropertyType, constantValue ) ) );
}

internal sealed class TrackModification<T> : ITrackModification
{
	public EditMode EditMode { get; }
	public MovieTrack Track { get; }

	private MovieTime? _originTime;

	private record ChangeMapping( MovieTimeRange TimeRange, IMovieBlock Original, IMovieBlock Change ) : IMovieBlock
	{
		private IMovieBlockData? _originalData;

		public IMovieBlockData? PreviewData { get; set; }

		public IMovieBlockData OriginalData => _originalData ??= Original.Data.Slice( TimeRange - Original.TimeRange.Start );

		IMovieBlockData IMovieBlock.Data => PreviewData ?? OriginalData;
	}

	private readonly List<IMovieBlock> _changes = new();
	private readonly List<ChangeMapping> _changeMappings = new();

	public bool HasChanges => _changes.Count > 0;

	public TrackModification( EditMode editMode, MovieTrack track )
	{
		EditMode = editMode;
		Track = track;
	}

	public void SetChanges( MovieTime? originTime, IEnumerable<IMovieBlock> blocks )
	{
		_originTime = originTime;

		_changes.Clear();
		_changes.AddRange( blocks );
	}

	public void ClearPreview()
	{
		_changeMappings.Clear();

		EditMode.ClearPreviewBlocks( Track );
	}

	private void UpdateChangeMappings( MovieTimeRange timeRange, MovieTime changeOffset )
	{
		_changeMappings.Clear();

		for ( var i = 0; i < _changes.Count; ++i )
		{
			var change = _changes[i];
			var changeTimeRange = change.TimeRange + changeOffset;

			// First / last change should be used outside the changed range

			if ( i == 0 )
			{
				changeTimeRange = (MovieTime.Min( changeTimeRange.Start, timeRange.Start ), changeTimeRange.End);
			}

			if ( i == _changes.Count - 1 )
			{
				changeTimeRange = (changeTimeRange.Start, MovieTime.Max( changeTimeRange.End, timeRange.End ));
			}

			if ( changeTimeRange.Intersect( timeRange ) is not { IsEmpty: false } intersection ) continue;

			var changeBlock = new MovieBlockSlice( change.TimeRange + changeOffset, change.Data );
			var anyCuts = false;

			foreach ( var cut in Track.GetCuts( intersection ) )
			{
				_changeMappings.Add( new ChangeMapping( cut.TimeRange, cut.Block, changeBlock ) );
				anyCuts = true;
			}

			if ( !anyCuts )
			{
				_changeMappings.Add( new ChangeMapping( intersection, changeBlock, changeBlock ) );
			}
		}
	}

	public bool Update( TimeSelection selection, MovieTime offset, bool additive )
	{
		if ( !HasChanges || !Track.CanModify() )
		{
			ClearPreview();
			return false;
		}

		var timeRange = selection.TotalTimeRange;
		var sampleRate = EditMode.Clip.DefaultSampleRate;

		UpdateChangeMappings( timeRange, offset );

		foreach ( var mapping in _changeMappings )
		{
			mapping.PreviewData = Blend( mapping.Original, mapping.Change, mapping.TimeRange, selection, additive, sampleRate );
		}

		EditMode.SetPreviewBlocks( Track, _changeMappings );

		return true;
	}

	public bool Commit( TimeSelection selection, MovieTime offset, bool additive )
	{
		if ( !Update( selection, offset, additive ) ) return false;

		var insertOptions = new InsertOptions( _changeMappings,
			StitchStart: selection.FadeIn.Duration.IsPositive,
			StitchEnd: selection.FadeOut.Duration.IsPositive );

		if ( !Track.Splice( selection.TotalTimeRange, selection.TotalTimeRange.Duration, insertOptions ) )
		{
			return false;
		}

		var stitchTimeRange = selection.PeakTimeRange.Grow(
			insertOptions.StitchStart ? MovieTime.Epsilon : MovieTime.Zero,
			insertOptions.StitchEnd ? MovieTime.Epsilon : MovieTime.Zero );

		MovieBlock? prevBlock = null;
		foreach ( var cut in Track.GetCuts( stitchTimeRange ).ToArray() )
		{
			// Stitch adjacent blocks if there isn't a cut in the original change

			prevBlock = prevBlock?.End == cut.Block.Start && _changes.All( x => x.TimeRange.Start + offset != cut.Block.Start )
				? Track.Stitch( prevBlock, cut.Block ) ?? cut.Block
				: cut.Block;
		}

		ClearPreview();

		_changeMappings.Clear();

		return true;
	}

	private IMovieBlockData Blend( IMovieBlock original, IMovieBlock change, MovieTimeRange timeRange, TimeSelection selection, bool additive, int sampleRate )
	{
		if ( original.Data is not IMovieBlockValueData<T> originalData ) return original.Data.Slice( timeRange );
		if ( change.Data is not IMovieBlockValueData<T> changeData ) return original.Data.Slice( timeRange );

		if ( additive ) throw new NotImplementedException();

		var interpolator = Interpolator.GetDefault<T>();
		var transformer = additive ? LocalTransformer.GetDefault<T>() : null;

		var sampleCount = timeRange.Duration.GetFrameCount( sampleRate );

		var dstValues = new T[sampleCount];

		originalData.Sample( dstValues, timeRange - original.TimeRange.Start, sampleRate );

		for ( var i = 0; i < sampleCount; ++i )
		{
			var time = timeRange.Start + MovieTime.FromFrames( i, sampleRate );
			var fade = selection.GetFadeValue( time );

			var src = dstValues[i];
			var dst = changeData.GetValue( time - change.TimeRange.Start );

			// todo: additive

			dstValues[i] = interpolator is null
				? fade >= 1f ? dst : src
				: interpolator.Interpolate( src, dst, fade );
		}

		return new SamplesData<T>( sampleRate, SampleInterpolationMode.Linear, dstValues );
	}
}
