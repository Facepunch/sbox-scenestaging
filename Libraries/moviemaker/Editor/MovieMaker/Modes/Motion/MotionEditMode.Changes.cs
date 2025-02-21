using System.Collections.Immutable;
using System.Linq;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

partial class MotionEditMode
{
	private bool _hasChanges;

	public override bool AllowTrackCreation => TimeSelection is not null;

	public bool HasChanges
	{
		get => _hasChanges;
		set
		{
			_hasChanges = value;
			SelectionChanged();
		}
	}

	public Color SelectionColor => (HasChanges ? Theme.Yellow : Theme.Blue).WithAlpha( 0.25f );

	/// <summary>
	/// Holds pending changes for a track.
	/// </summary>
	private sealed class TrackState
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

		public TrackModifier? Modifier { get; }

		public TrackState( EditMode editMode, MovieTrack track )
		{
			EditMode = editMode;
			Track = track;
			Modifier = TrackModifier.Get( track.PropertyType );
		}

		public bool TryGetLocalValue( MovieTime time, object? globalValue, out object? localValue )
		{
			localValue = null;

			if ( LocalTransformer.GetDefault( Track.PropertyType ) is not { } transformer ) return false;
			if ( !Track.TryGetValue( time, out var relativeTo ) ) return false;

			localValue = transformer.ToLocal( globalValue, relativeTo );
			return true;
		}

		public void SetChanges( MovieTime? originTime, IEnumerable<IMovieBlock> blocks )
		{
			_originTime = originTime;

			_changes.Clear();
			_changes.AddRange( blocks );
		}

		public void SetChanges( MovieTime? originTime, params IMovieBlock[] blocks ) => SetChanges( originTime, blocks.AsEnumerable() );

		public void SetChanges( MovieTime? originTime, object? constantValue )
		{
			if ( Modifier is not { } modifier ) return;

			var block = new MovieBlockSlice( (MovieTime.Zero, MovieTime.MaxValue), modifier.GetConstant( constantValue ) );

			SetChanges( originTime, block );
		}

		public void ClearPreview()
		{
			_changeMappings.Clear();

			EditMode.ClearPreviewBlocks( Track );
		}

		private void UpdateChangeMappings( MovieTimeRange timeRange, MovieTime changeOffset )
		{
			_changeMappings.Clear();

			foreach ( var change in _changes )
			{
				var changeTimeRange = change.TimeRange + changeOffset;

				if ( changeTimeRange.Intersect( timeRange ) is not { IsEmpty: false } intersection ) continue;

				foreach ( var cut in Track.GetCuts( intersection ) )
				{
					_changeMappings.Add( new ChangeMapping( cut.TimeRange, cut.Block, new MovieBlockSlice( changeTimeRange, change.Data ) ) );
				}
			}
		}

		public bool Update( TimeSelection selection, bool additive )
		{
			if ( !HasChanges || Modifier is not { } modifier )
			{
				ClearPreview();
				return false;
			}

			if ( additive ) throw new NotImplementedException();

			var timeRange = selection.PeakTimeRange;
			var sampleRate = EditMode.Clip.DefaultSampleRate;

			UpdateChangeMappings( timeRange, timeRange.Start );

			foreach ( var mapping in _changeMappings )
			{
				mapping.PreviewData = modifier.Blend( mapping.Original, mapping.Change, mapping.TimeRange, selection, additive, sampleRate );
			}

			EditMode.SetPreviewBlocks( Track, _changeMappings );

			return true;
		}

		public bool Commit( TimeSelection selection, bool additive )
		{
			if ( !Update( selection, additive ) ) return false;

			throw new NotImplementedException();

			// Track.Splice( _sampleRange.TimeRange, _sampleRange.TimeRange.Duration, [modifiedData] );

			return true;
		}
	}

	private Dictionary<MovieTrack, TrackState> TrackStates { get; } = new();

	private void ClearChanges()
	{
		foreach ( var state in TrackStates.Values )
		{
			state.ClearPreview();
		}

		TrackStates.Clear();
		HasChanges = false;
	}

	private void CommitChanges()
	{
		if ( TimeSelection is not { } selection ) return;

		foreach ( var (track, state) in TrackStates )
		{
			state.Commit( selection, IsAdditive );
		}

		TrackStates.Clear();
		HasChanges = false;

		Session.ClipModified();
	}

	protected override void OnDelete()
	{
		var shift = (Application.KeyboardModifiers & KeyboardModifiers.Shift) != 0;

		Delete( shift );
	}

	protected override void OnInsert()
	{
		if ( TimeSelection is not { } selection ) return;

		Session.Insert( selection.PeakTimeRange );
	}

	private void Delete( bool shift )
	{
		if ( TimeSelection is not { } selection ) return;

		ClearChanges();

		Session.Delete( selection.PeakTimeRange, shift );
	}

	private record ClipboardData( MovieTime Duration, IReadOnlyDictionary<Guid, IReadOnlyList<MovieBlockSlice>> Tracks );

	private static ClipboardData? Clipboard { get; set; }

	protected override void OnSelectAll()
	{
		TimeSelection = new TimeSelection();
	}

	protected override void OnCut()
	{
		OnCopy();
		Delete( true );
	}

	protected override void OnCopy()
	{
		if ( TimeSelection is not { } selection || Session.Clip is not { } clip ) return;

		var timeRange = selection.PeakTimeRange;
		var tracks = new Dictionary<Guid, IReadOnlyList<MovieBlockSlice>>();
		var slicedBlocks = new List<MovieBlockSlice>();

		foreach ( var track in clip.AllTracks )
		{
			slicedBlocks.Clear();
			slicedBlocks.AddRange( track.Slice( timeRange ).Select( x => x with { TimeRange = x.TimeRange - timeRange.Start } ) );

			if ( slicedBlocks.Count > 0 )
			{
				tracks[track.Id] = slicedBlocks.ToImmutableList();
			}
		}

		Clipboard = new ClipboardData( timeRange.Duration, tracks.ToImmutableDictionary() );
	}

	protected override void OnPaste()
	{
		if ( Session.Clip is not { } clip || Clipboard is not { } clipboard ) return;

		ClearChanges();

		if ( TimeSelection is not { } selection )
		{
			TimeSelection = selection = new TimeSelection( (Session.CurrentPointer, Session.CurrentPointer + clipboard.Duration), DefaultInterpolation );
		}

		foreach ( var (id, blocks) in clipboard.Tracks )
		{
			if ( clip.GetTrack( id ) is not { } track ) continue;

			var state = GetOrCreateTrackState( track );

			state.SetChanges( Session.CurrentPointer, blocks );

			HasChanges = true;

			state.Update( selection, IsAdditive );
		}
	}

	private TrackState? GetTrackState( MovieTrack track )
	{
		return TrackStates!.GetValueOrDefault( track );
	}

	private TrackState GetOrCreateTrackState( MovieTrack track )
	{
		if ( GetTrackState( track ) is { } state ) return state;

		TrackStates.Add( track, state = new TrackState( this, track ) );

		return state;
	}

	protected override bool OnPreChange( DopeSheetTrack track )
	{
		if ( TimeSelection is not { } selection ) return false;
		if ( track.TrackWidget.Property is not { } property )
		{
			return false;
		}

		var movieTrack = track.TrackWidget.MovieTrack;

		if ( TrackStates.ContainsKey( movieTrack ) )
		{
			return false;
		}

		var state = TrackStates[movieTrack] = new TrackState( this, movieTrack );

		if ( state.Modifier is null )
		{
			Log.Warning( $"Can't motion edit tracks of type '{movieTrack.PropertyType}'." );
		}

		return true;
	}

	protected override bool OnPostChange( DopeSheetTrack track )
	{
		if ( TimeSelection is not { } selection ) return false;

		var movieTrack = track.TrackWidget.MovieTrack;

		if ( track.TrackWidget.Property is not { } property )
		{
			return false;
		}

		if ( GetTrackState( movieTrack ) is not { } state )
		{
			return false;
		}

		state.SetChanges( Session.CurrentPointer, property.Value );

		HasChanges = true;

		return state.Update( selection, IsAdditive );
	}

	private bool SetChanges( MovieTrack track, IEnumerable<IMovieBlock> changes )
	{
		if ( TimeSelection is not { } selection ) return false;

		if ( !TrackStates.TryGetValue( track, out var state ) )
		{
			state = new TrackState( this, track );
		}

		state.SetChanges( Session.CurrentPointer, changes );

		HasChanges = true;

		return state.Update( selection, IsAdditive );
	}

	private bool _hasSelectionItems;

	private void SelectionChanged()
	{
		if ( TimeSelection is { } selection )
		{
			foreach ( var (track, state) in TrackStates )
			{
				if ( !state.Update( selection, IsAdditive ) ) continue;

				TrackList.FindTrack( track )?.DopeSheetTrack?.UpdateBlockItems();
			}

			if ( !_hasSelectionItems )
			{
				_hasSelectionItems = true;

				DopeSheet.Add( new TimeSelectionPeakItem( this ) );

				DopeSheet.Add( new TimeSelectionFadeItem( this, FadeKind.FadeIn ) );
				DopeSheet.Add( new TimeSelectionFadeItem( this, FadeKind.FadeOut ) );

				DopeSheet.Add( new TimeSelectionHandleItem( this ) );
				DopeSheet.Add( new TimeSelectionHandleItem( this ) );
				DopeSheet.Add( new TimeSelectionHandleItem( this ) );
				DopeSheet.Add( new TimeSelectionHandleItem( this ) );
			}

			foreach ( var item in DopeSheet.Items.OfType<TimeSelectionItem>() )
			{
				item.UpdatePosition( selection, DopeSheet.VisibleRect );
			}
		}
		else if ( _hasSelectionItems )
		{
			_hasSelectionItems = false;

			foreach ( var item in DopeSheet.Items.OfType<TimeSelectionItem>().ToArray() )
			{
				item.Destroy();
			}
		}
	}

	protected override void OnViewChanged( Rect viewRect )
	{
		if ( TimeSelection is not { } selection ) return;

		foreach ( var item in DopeSheet.Items.OfType<TimeSelectionItem>() )
		{
			item.UpdatePosition( selection, viewRect );
		}
	}
}
