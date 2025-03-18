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

	private Dictionary<IProjectTrack, ITrackModification> TrackModifications { get; } = new();

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
			state.Commit( new ModificationOptions( selection, ChangeOffset, IsAdditive, SmoothingSize ) );
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

		var changed = false;

		foreach ( var track in Session.EditableTracks )
		{
			if ( shift )
			{
				changed |= track.Remove( selection.PeakTimeRange ) && Session.TrackModified( track );
			}
			else
			{
				changed |= track.Clear( selection.PeakTimeRange ) && Session.TrackModified( track );
			}
		}

		if ( changed )
		{
			Session.ClipModified();
			DisplayAction( "delete" );
		}
	}

	protected override void OnInsert()
	{
		if ( TimeSelection is not { } selection ) return;

		var changed = false;

		foreach ( var track in Session.EditableTracks )
		{
			changed |= track.Insert( selection.PeakTimeRange ) && Session.TrackModified( track );
		}

		if ( changed )
		{
			DisplayAction( "keyboard_tab" );
		}
	}

	private record ClipboardData( TimeSelection Selection, IReadOnlyDictionary<Guid, IReadOnlyList<IProjectPropertyBlock>> Tracks );

	private static ClipboardData? Clipboard { get; set; }

	protected override void OnSelectAll()
	{
		TimeSelection = new TimeSelection( (MovieTime.Zero, Project.Duration), DefaultInterpolation );
	}

	protected override void OnCut()
	{
		OnCopy();
		Delete( true );

		DisplayAction( "content_cut" );
	}

	protected override void OnCopy()
	{
		if ( TimeSelection is not { } selection ) return;

		var timeRange = selection.TotalTimeRange;
		var offset = Session.CurrentPointer;
		var tracks = new Dictionary<Guid, IReadOnlyList<IProjectPropertyBlock>>();
		var slicedBlocks = new List<IProjectPropertyBlock>();

		foreach ( var track in Session.EditableTracks )
		{
			slicedBlocks.Clear();
			slicedBlocks.AddRange( track.Slice( timeRange ).Select( x => x.Shift( -offset ) ) );

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
		if ( Session.Project is not { } clip || Clipboard is not { } clipboard ) return false;

		ClearChanges();

		var selection = clipboard.Selection + Session.CurrentPointer;
		var pasteTime = selection.TotalStart;

		TimeSelection = selection;
		ChangeOffset = pasteTime;

		var changed = false;

		foreach ( var (id, blocks) in clipboard.Tracks )
		{
			if ( blocks.Count == 0 ) continue;
			if ( clip.GetTrack( id ) is not { } track ) continue;

			var state = GetOrCreateTrackModification( track );

			// Additive blending is relative to the start of the first block

			state.SetRelativeTo( blocks[0].GetValue( MovieTime.Zero ) );
			state.SetOverlay( blocks, -clipboard.Selection.TotalStart );
			state.Update( new ModificationOptions( selection, ChangeOffset, IsAdditive, SmoothingSize ) );

			changed = true;
		}

		_changeDuration = changed ? clipboard.Selection.TotalTimeRange.Duration : null;
		HasChanges = changed;

		return changed;
	}

	private ITrackModification? GetTrackModification( IProjectTrack track )
	{
		return TrackModifications!.GetValueOrDefault( track );
	}

	private ITrackModification GetOrCreateTrackModification( IProjectTrack track )
	{
		if ( GetTrackModification( track ) is { } state ) return state;

		var type = typeof(TrackModification<>).MakeGenericType( track.TargetType );
		TrackModifications.Add( track, state = (ITrackModification)Activator.CreateInstance( type, this, track )! );

		return state;
	}

	protected override void OnTrackStateChanged( DopeSheetTrack track )
	{
		if ( TimeSelection is not { } selection ) return;

		if ( GetTrackModification( track.TrackWidget.ProjectTrack ) is { } state )
		{
			state.Update( new ModificationOptions( selection, ChangeOffset, IsAdditive, SmoothingSize ) );
		}
	}

	protected override bool OnPreChange( DopeSheetTrack track )
	{
		if ( TimeSelection is not { } selection ) return false;
		if ( track.TrackWidget.Target is not { } property )
		{
			return false;
		}

		var movieTrack = track.TrackWidget.ProjectTrack;

		if ( TrackModifications.ContainsKey( movieTrack ) )
		{
			return false;
		}

		// We create modifications in PreChange so we can capture the pre-change value,
		// used for additive blending

		var modification = GetOrCreateTrackModification( movieTrack );

		modification.SetRelativeTo( property.Value );

		return true;
	}

	protected override bool OnPostChange( DopeSheetTrack track )
	{
		if ( TimeSelection is not { } selection ) return false;

		var movieTrack = track.TrackWidget.ProjectTrack;

		if ( track.TrackWidget.Target is not { } property )
		{
			return false;
		}

		if ( GetTrackModification( movieTrack ) is not { } state )
		{
			return false;
		}

		state.SetOverlay( property.Value );

		HasChanges = true;

		return state.Update( new ModificationOptions( selection, ChangeOffset, IsAdditive, SmoothingSize ) );
	}

	private bool _hasSelectionItems;

	private void SelectionChanged()
	{
		if ( _smoothSlider is { } slider )
		{
			slider.Slider.Hidden = !SmoothingEnabled;
			slider.Label.Hidden = !SmoothingEnabled;
		}

		if ( TimeSelection is { } selection )
		{
			PasteTimeRange = _changeDuration is { } duration ? (ChangeOffset, ChangeOffset + duration) : null;

			foreach ( var (_, state) in TrackModifications )
			{
				state.Update( new ModificationOptions( selection, ChangeOffset, IsAdditive, SmoothingSize ) );
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
		RecordingFrame();

		if ( _lastActionTime < 1f )
		{
			UpdateSelectionItems( DopeSheet.VisibleRect );
		}
	}
}
