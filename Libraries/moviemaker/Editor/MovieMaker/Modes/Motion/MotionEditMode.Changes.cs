using System.Collections.Immutable;
using System.Linq;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

partial class MotionEditMode
{
	private bool _hasChanges;
	private MovieTime? _changeDuration;

	private RealTimeSince _lastActionTime;

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

	public Color SelectionColor
	{
		get
		{
			var t = MathF.Pow( Math.Clamp( 1f - _lastActionTime, 0f, 1f ), 8f );
			var color = (HasChanges ? Theme.Yellow : Theme.Blue).WithAlpha( 0.25f );

			return Color.Lerp( color, Theme.White.WithAlpha( 0.5f ), t );
		}
	}

	public string? LastActionIcon { get; private set; }

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
			if ( Modifier is null ) return;

			SetChanges( originTime, new MovieBlockSlice( (MovieTime.Zero, MovieTime.MaxValue), EditHelpers.CreateConstantData( Track.PropertyType, constantValue ) ) );
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
			if ( !HasChanges || Modifier is not { } modifier || !Track.CanModify() )
			{
				ClearPreview();
				return false;
			}

			if ( additive ) throw new NotImplementedException();

			var timeRange = selection.TotalTimeRange;
			var sampleRate = EditMode.Clip.DefaultSampleRate;

			UpdateChangeMappings( timeRange, offset );

			foreach ( var mapping in _changeMappings )
			{
				mapping.PreviewData = modifier.Blend( mapping.Original, mapping.Change, mapping.TimeRange, selection, additive, sampleRate );
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
	}

	private Dictionary<MovieTrack, TrackState> TrackStates { get; } = new();

	private void ClearChanges()
	{
		if ( !HasChanges ) return;

		foreach ( var state in TrackStates.Values )
		{
			state.ClearPreview();
		}

		_changeDuration = null;

		TrackStates.Clear();
		HasChanges = false;

		DisplayAction( "clear" );
	}

	private void CommitChanges()
	{
		if ( TimeSelection is not { } selection || !HasChanges ) return;

		foreach ( var (_, state) in TrackStates )
		{
			state.Commit( selection, ChangeOffset, IsAdditive );
		}

		_changeDuration = null;

		DisplayAction( "approval" );

		TrackStates.Clear();
		HasChanges = false;

		Session.ClipModified();
	}

	protected override void OnDelete( bool shift )
	{
		if ( TimeSelection is not { } selection ) return;

		if ( Session.Delete( selection.PeakTimeRange, shift ) )
		{
			DisplayAction( "delete" );
		}
	}

	protected override void OnInsert()
	{
		if ( TimeSelection is not { } selection ) return;

		if ( Session.Insert( selection.PeakTimeRange ) )
		{
			DisplayAction( "keyboard_tab" );
		}
	}

	private record ClipboardData( TimeSelection Selection, IReadOnlyDictionary<Guid, IReadOnlyList<MovieBlockSlice>> Tracks );

	private static ClipboardData? Clipboard { get; set; }

	protected override void OnSelectAll()
	{
		TimeSelection = new TimeSelection( (MovieTime.Zero, Clip.Duration), DefaultInterpolation );
	}

	protected override void OnCut()
	{
		OnCopy();
		Delete( true );

		DisplayAction( "content_cut" );
	}

	protected override void OnCopy()
	{
		if ( TimeSelection is not { } selection || Session.Clip is not { } clip ) return;

		var timeRange = selection.TotalTimeRange;
		var offset = Session.CurrentPointer;
		var tracks = new Dictionary<Guid, IReadOnlyList<MovieBlockSlice>>();
		var slicedBlocks = new List<MovieBlockSlice>();

		foreach ( var track in clip.AllTracks )
		{
			if ( !track.CanModify() ) continue;

			slicedBlocks.Clear();
			slicedBlocks.AddRange( track.Slice( timeRange ).Select( x => x with { TimeRange = x.TimeRange - offset } ) );

			if ( slicedBlocks.Count > 0 )
			{
				tracks[track.Id] = slicedBlocks.ToImmutableList();
			}
		}

		if ( tracks.Count <= 0 ) return;

		Clipboard = new ClipboardData( selection - offset, tracks.ToImmutableDictionary() );

		if ( LoadChangesFromClipboard() )
		{
			DisplayAction( "content_copy" );
		}
	}

	protected override void OnPaste()
	{
		if ( LoadChangesFromClipboard() )
		{
			DisplayAction( "content_paste" );
		}
	}

	private bool LoadChangesFromClipboard()
	{
		if ( Session.Clip is not { } clip || Clipboard is not { } clipboard ) return false;

		ClearChanges();

		var selection = clipboard.Selection + Session.CurrentPointer;
		var pasteTime = selection.TotalStart;

		TimeSelection = selection;
		ChangeOffset = pasteTime;

		var changed = false;

		foreach ( var (id, blocks) in clipboard.Tracks )
		{
			if ( clip.GetTrack( id ) is not { } track ) continue;

			var state = GetOrCreateTrackState( track );

			state.SetChanges( Session.CurrentPointer, blocks.Select( x => x with { TimeRange = x.TimeRange - clipboard.Selection.TotalStart } ) );
			state.Update( selection, ChangeOffset, IsAdditive );

			changed = true;
		}

		_changeDuration = changed ? clipboard.Selection.TotalTimeRange.Duration : null;
		HasChanges = changed;

		return changed;
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

	protected override void OnTrackStateChanged( DopeSheetTrack track )
	{
		if ( TimeSelection is not { } selection ) return;

		if ( GetTrackState( track.TrackWidget.MovieTrack ) is { } state )
		{
			state.Update( selection, ChangeOffset, IsAdditive );
		}
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

		return state.Update( selection, ChangeOffset, IsAdditive );
	}

	private bool _hasSelectionItems;

	private void SelectionChanged()
	{
		if ( TimeSelection is { } selection )
		{
			PasteTimeRange = _changeDuration is { } duration ? (ChangeOffset, ChangeOffset + duration) : null;

			foreach ( var (track, state) in TrackStates )
			{
				state.Update( selection, ChangeOffset, IsAdditive );
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

			UpdateSelectionItems( DopeSheet.VisibleRect );
		}
		else if ( _hasSelectionItems )
		{
			_hasSelectionItems = false;

			PasteTimeRange = null;

			foreach ( var item in DopeSheet.Items.OfType<TimeSelectionItem>().ToArray() )
			{
				item.Destroy();
			}
		}

		Session.RefreshNextFrame();
	}

	protected override void OnViewChanged( Rect viewRect )
	{
		UpdateSelectionItems( viewRect );
	}

	private void UpdateSelectionItems( Rect viewRect )
	{
		if ( TimeSelection is not { } selection ) return;

		foreach ( var item in DopeSheet.Items.OfType<TimeSelectionItem>() )
		{
			item.UpdatePosition( selection, viewRect );
		}
	}

	public void DisplayAction( string icon )
	{
		_lastActionTime = 0f;
		LastActionIcon = icon;

		UpdateSelectionItems( DopeSheet.VisibleRect );
	}

	protected override void OnFrame()
	{
		if ( _lastActionTime < 1f )
		{
			UpdateSelectionItems( DopeSheet.VisibleRect );
		}
	}
}
