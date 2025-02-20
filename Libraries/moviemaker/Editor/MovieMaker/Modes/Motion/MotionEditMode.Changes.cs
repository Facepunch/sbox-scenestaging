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
	/// Captures the state of a track before it was modified, and records any pending changes.
	/// </summary>
	private sealed class TrackState : IMovieBlock
	{
		public MovieTrack Track { get; }
		public IMovieBlockData Data => _modifiedData!;
		public MovieTimeRange TimeRange => _sampleRange.TimeRange;

		private object? _changedValue;

		private record struct SampleRange( MovieTimeRange TimeRange, int SampleRate );
		private SampleRange _sampleRange;
		private ISamplesData? _originalData;
		private ISamplesData? _modifiedData;

		public object? ChangedValue
		{
			get => _changedValue;
			set
			{
				_changedValue = value;
				HasChanges = true;
			}
		}

		public bool IsAdditive { get; set; }

		public bool HasChanges { get; private set; }
		public bool HasPreview => HasChanges && _modifiedData is not null;

		public TrackModifier? Modifier { get; }

		public TrackState( MovieTrack track )
		{
			Track = track;
			Modifier = TrackModifier.Get( track.PropertyType );
		}

		private bool TryGetOriginalValue( MovieTime time, out object? value )
		{
			if ( _originalData is { } original )
			{
				value = original.GetValue( time );
				return true;
			}

			value = null;
			return false;
		}

		public bool TryGetLocalValue( MovieTime time, object? globalValue, out object? localValue )
		{
			localValue = null;

			if ( LocalTransformer.GetDefault( Track.PropertyType ) is not { } transformer ) return false;
			if ( !TryGetOriginalValue( time, out var relativeTo ) ) return false;

			localValue = transformer.ToLocal( globalValue, relativeTo );
			return true;
		}

		public void ClearPreview()
		{
			_sampleRange = default;
			_originalData = null;
			_modifiedData = null;
		}

		private SampleRange GetAffectedSampleRange( TimeSelection selection, int sampleRate )
		{
			var timeRange = selection.GetTimeRange( Track.Clip );

			// If we fade in / out, include the block(s) the fade is overlapping so we have
			// a smooth transition instead of a hard cut

			foreach ( var block in Track.Blocks )
			{
				if ( selection.FadeIn is { Duration.IsZero: false } fadeIn )
				{
					if ( block.TimeRange.Intersect( fadeIn.TimeRange ) is { IsEmpty: false } )
					{
						timeRange = timeRange.Union( block.Start );
					}
				}

				if ( selection.FadeOut is { Duration.IsZero: false } fadeOut )
				{
					if ( block.TimeRange.Intersect( fadeOut.TimeRange ) is { IsEmpty: false } )
					{
						timeRange = timeRange.Union( block.End );
					}
				}
			}

			return new SampleRange( timeRange, sampleRate );
		}

		public bool Update( TimeSelection selection, int sampleRate )
		{
			if ( !HasChanges || Modifier is not { } modifier )
			{
				ClearPreview();
				return false;
			}

			var range = GetAffectedSampleRange( selection, sampleRate );

			if ( _sampleRange != range )
			{
				_sampleRange = range;

				_originalData = modifier.SampleTrack( Track, range.TimeRange, sampleRate );
				_modifiedData = null;
			}

			if ( _originalData is { } originalData )
			{
				_modifiedData = modifier.Modify( originalData, _sampleRange.TimeRange, selection, ChangedValue, IsAdditive );
			}

			return true;
		}

		public bool Commit( TimeSelection selection, int sampleRate )
		{
			if ( !Update( selection, sampleRate ) || _modifiedData is not { } modifiedData ) return false;

			Track.Replace( _sampleRange.TimeRange, modifiedData );

			return true;
		}
	}

	private Dictionary<MovieTrack, TrackState> TrackStates { get; } = new();

	private void ClearChanges()
	{
		foreach ( var (track, state) in TrackStates )
		{
			if ( !track.IsValid ) continue;

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
			state.Commit( selection, Session.FrameRate );
		}

		TrackStates.Clear();
		HasChanges = false;

		Session.ClipModified();
	}

	protected override void OnDelete()
	{
		if ( TimeSelection is not { } selection || Session.Clip is not { } clip ) return;

		var timeRange = selection.GetPeakTimeRange( clip );

		ClearChanges();

		var shift = (Application.KeyboardModifiers & KeyboardModifiers.Shift) != 0;

		foreach ( var track in clip.AllTracks )
		{
			if ( track.Delete( timeRange, shift ) )
			{
				Session.TrackModified( track );
			}
		}

		Session.ClipModified();
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

		var state = TrackStates[movieTrack] = new TrackState( movieTrack );

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

		if ( !TrackStates.TryGetValue( movieTrack, out var state ) )
		{
			return false;
		}

		var globalValue = property.Value;

		if ( IsAdditive && state.TryGetLocalValue( Session.CurrentPointer, globalValue, out var localValue ) )
		{
			state.IsAdditive = true;
			state.ChangedValue = localValue;
		}
		else
		{
			state.IsAdditive = false;
			state.ChangedValue = property.Value;
		}

		HasChanges = true;

		return state.Update( selection, Session.FrameRate );
	}

	protected override IEnumerable<IMovieBlock> OnGetPreviewBlocks()
	{
		return HasChanges
			? TrackStates.Values.Where( x => x.HasPreview )
			: [];
	}

	private bool _hasSelectionItems;

	private void SelectionChanged()
	{
		if ( TimeSelection is { } selection )
		{
			foreach ( var (track, state) in TrackStates )
			{
				if ( !state.Update( selection, Session.FrameRate ) ) continue;

				TrackList.FindTrack( track )?.DopeSheetTrack?.UpdateBlockPreviews();
			}

			if ( !_hasSelectionItems )
			{
				_hasSelectionItems = true;

				DopeSheet.Add( new TimeSelectionPeakItem( this ) );
				DopeSheet.Add( new TimeSelectionFadeItem( this, FadeKind.FadeIn ) );
				DopeSheet.Add( new TimeSelectionFadeItem( this, FadeKind.FadeOut ) );

				// Peak edge handles

				DopeSheet.Add( new TimeSelectionHandleItem( this, value => value.FadeIn is { } fadeIn ? fadeIn.End : null, ( value, time ) => value.WithPeakStart( time, DefaultInterpolation, false ) ) );
				DopeSheet.Add( new TimeSelectionHandleItem( this, value => value.FadeOut is { } fadeOut ? fadeOut.Start : null, ( value, time ) => value.WithPeakEnd( time, DefaultInterpolation, false ) ) );

				// Fade edge handles

				DopeSheet.Add( new TimeSelectionHandleItem( this, value => value.FadeIn is { } fadeIn ? fadeIn.Start : null, (value, time) => value.WithFadeStart( time ) ) );
				DopeSheet.Add( new TimeSelectionHandleItem( this, value => value.FadeOut is { } fadeOut ? fadeOut.End : null, ( value, time ) => value.WithFadeEnd( time ) ) );
			}

			foreach ( var item in DopeSheet.Items.OfType<ITimeSelectionItem>() )
			{
				item.UpdatePosition( selection, DopeSheet.VisibleRect );
			}
		}
		else if ( _hasSelectionItems )
		{
			_hasSelectionItems = false;

			foreach ( var item in DopeSheet.Items.OfType<ITimeSelectionItem>().ToArray() )
			{
				item.Destroy();
			}
		}
	}

	protected override void OnViewChanged( Rect viewRect )
	{
		if ( TimeSelection is not { } selection ) return;

		foreach ( var item in DopeSheet.Items.OfType<ITimeSelectionItem>() )
		{
			item.UpdatePosition( selection, viewRect );
		}
	}

	private void SelectAll()
	{
		TimeSelection = new TimeSelection();
	}
}
