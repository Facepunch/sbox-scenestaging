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

		private record struct SampleRange( float Start, float Duration, int SampleRate );
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

		private bool TryGetOriginalValue( float time, out object? value )
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

		public bool TryGetLocalValue( float time, object? globalValue, out object? localValue )
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
			var min = selection.Start?.FadeTime ?? 0f;
			var max = selection.End?.FadeTime ?? Track.Clip.Duration;

			foreach ( var block in Track.Blocks )
			{
				if ( selection.Start is { Duration: > 0 } fadeIn )
				{
					if ( (Min: fadeIn.FadeTime, Max: fadeIn.PeakTime).Overlaps( block.StartTime, block.EndTime ) )
					{
						min = Math.Min( min, block.StartTime );
					}
				}

				if ( selection.End is { Duration: > 0 } fadeOut )
				{
					if ( (Min: fadeOut.PeakTime, Max: fadeOut.FadeTime).Overlaps( block.StartTime, block.EndTime ) )
					{
						max = Math.Max( max, block.EndTime );
					}
				}
			}

			return new SampleRange( min, max - min, sampleRate );
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
				_originalData = modifier.SampleTrack( Track, range.Start, range.Duration, sampleRate );
				_previewBlock = Track.AddBlock( range.Start, range.Duration, _originalData );
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
				.Where( x => x.StartTime >= previewBlock.StartTime && x.EndTime <= previewBlock.EndTime )
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

				DopeSheet.Add( new TimeSelectionHandleItem( this, value => value.Start is { } start ? start.PeakTime : null, ( value, time ) => value.WithPeakStart( time, DefaultInterpolation, false ) ) );
				DopeSheet.Add( new TimeSelectionHandleItem( this, value => value.End is { } end ? end.PeakTime : null, ( value, time ) => value.WithPeakEnd( time, DefaultInterpolation, false ) ) );

				// Fade edge handles

				DopeSheet.Add( new TimeSelectionHandleItem( this, value => value.Start is { } start ? start.FadeTime : null, (value, time) => value.WithFadeStart( time ) ) );
				DopeSheet.Add( new TimeSelectionHandleItem( this, value => value.End is { } end ? end.FadeTime : null, ( value, time ) => value.WithFadeEnd( time ) ) );
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
