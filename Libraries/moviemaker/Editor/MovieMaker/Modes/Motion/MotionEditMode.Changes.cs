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

	private Dictionary<MovieTrack, ITrackModification> TrackModifications { get; } = new();

	private void ClearChanges()
	{
		if ( !HasChanges ) return;

		foreach ( var state in TrackModifications.Values )
		{
			state.ClearPreview();
		}

		_changeDuration = null;

		TrackModifications.Clear();
		HasChanges = false;

		DisplayAction( "clear" );
	}

	private void CommitChanges()
	{
		if ( TimeSelection is not { } selection || !HasChanges ) return;

		foreach ( var (_, state) in TrackModifications )
		{
			state.Commit( selection, ChangeOffset, IsAdditive );
		}

		_changeDuration = null;

		DisplayAction( "approval" );

		TrackModifications.Clear();
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

			var state = GetOrCreateTrackModification( track );

			state.SetChanges( Session.CurrentPointer, blocks.Select( x => x with { TimeRange = x.TimeRange - clipboard.Selection.TotalStart } ) );
			state.Update( selection, ChangeOffset, IsAdditive );

			changed = true;
		}

		_changeDuration = changed ? clipboard.Selection.TotalTimeRange.Duration : null;
		HasChanges = changed;

		return changed;
	}

	private ITrackModification? GetTrackModification( MovieTrack track )
	{
		return TrackModifications!.GetValueOrDefault( track );
	}

	private ITrackModification GetOrCreateTrackModification( MovieTrack track )
	{
		if ( GetTrackModification( track ) is { } state ) return state;

		var type = typeof(TrackModification<>).MakeGenericType( track.PropertyType );
		TrackModifications.Add( track, state = (ITrackModification)Activator.CreateInstance( type, this, track )! );

		return state;
	}

	protected override void OnTrackStateChanged( DopeSheetTrack track )
	{
		if ( TimeSelection is not { } selection ) return;

		if ( GetTrackModification( track.TrackWidget.MovieTrack ) is { } state )
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

		if ( TrackModifications.ContainsKey( movieTrack ) )
		{
			return false;
		}

		GetOrCreateTrackModification( movieTrack );
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

		if ( GetTrackModification( movieTrack ) is not { } state )
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

			foreach ( var (track, state) in TrackModifications )
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
