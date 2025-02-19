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
	private sealed class TrackState
	{
		public MovieTrack Track { get; }

		private object? _changedValue;

		private record struct SampleRange( MovieTimeRange TimeRange, int SampleRate );
		private SampleRange _sampleRange;
		private MovieBlockData? _originalData;
		private MovieBlock? _previewBlock;

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

		public TrackModifier? Modifier { get; }

		public TrackState( MovieTrack track )
		{
			Track = track;
			Modifier = TrackModifier.Get( track.PropertyType );
		}

		private bool TryGetOriginalValue( MovieTime time, out object? value )
		{
			switch ( _originalData )
			{
				case ISamplesData samples:
					value = samples.GetValue( time );
					return true;

				case IConstantData constant:
					value = constant.Value;
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

			_previewBlock?.Remove();
			_previewBlock = null;
		}

		private SampleRange GetAffectedSampleRange( TimeSelection selection, int sampleRate )
		{
			var timeRange = selection.GetTimeRange( Track.Clip );

			foreach ( var block in Track.Blocks )
			{
				if ( selection.FadeIn is { Duration.IsZero: false } fadeIn )
				{
					if ( block.TimeRange.Intersect( fadeIn.TimeRange ) is not null )
					{
						timeRange = timeRange.Union( block.Start );
					}
				}

				if ( selection.FadeOut is { Duration.IsZero: false } fadeOut )
				{
					if ( block.TimeRange.Intersect( fadeOut.TimeRange ) is not null )
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

			if ( _sampleRange != range || _previewBlock is null || _originalData is null )
			{
				_sampleRange = range;

				_previewBlock?.Remove();
				_originalData = modifier.SampleTrack( Track, range.TimeRange, sampleRate );
				_previewBlock = Track.AddBlock( range.TimeRange, _originalData );
			}

			_previewBlock.Data = modifier.Modify( _previewBlock, _originalData, selection, ChangedValue, IsAdditive );

			return true;
		}

		public bool Commit( TimeSelection selection, int sampleRate )
		{
			if ( !Update( selection, sampleRate ) || _previewBlock is not { } previewBlock ) return false;

			// Remove all blocks completely masked by the edit

			var maskedBlocks = Track.Blocks
				.Where( x => x != previewBlock )
				.Where( x => previewBlock.TimeRange.Contains( x.TimeRange ) )
				.ToArray();

			foreach ( var block in maskedBlocks )
			{
				block.Remove();
			}

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
		if ( TimeSelection is not { } selection )
		{
			return;
		}

		foreach ( var (track, state) in TrackStates )
		{
			state.Commit( selection, Session.FrameRate );
		}

		TrackStates.Clear();
		HasChanges = false;

		Session.Current?.ClipModified();
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
}
