using System.Collections.Immutable;
using System.Linq;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

partial class MotionEditMode
{
	private bool _hasChanges;
	private MovieTime? _changeDuration;
	private ITrackModificationOptions? _options;

	private RealTimeSince _lastActionTime;

	public override bool AllowTrackCreation => TimeSelection is not null;

	public ITrackModificationOptions? ModificationOptions
	{
		get => _options;

		set
		{
			_options = value;
			SelectionChanged();
		}
	}

	public bool HasChanges => TrackModificationPreviews.Values.Any( x => x.Modification is not null );

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

	private Dictionary<IProjectPropertyTrack, ITrackModificationPreview> TrackModificationPreviews { get; } = new();

	private void ClearChanges()
	{
		if ( !HasChanges ) return;

		foreach ( var state in TrackModificationPreviews.Values )
		{
			state.Clear();
		}

		_changeDuration = null;

		TrackModificationPreviews.Clear();

		DisplayAction( "clear" );
	}

	private void CommitChanges()
	{
		if ( TimeSelection is not { } selection || ModificationOptions is not { } options || !HasChanges ) return;

		using ( PushTrackModification( "Commit", true ) )
		{
			foreach ( var (_, state) in TrackModificationPreviews )
			{
				state.Commit( selection, options );
			}

			TrackModificationPreviews.Clear();
		}

		_changeDuration = null;

		DisplayAction( "approval" );

		Session.ClipModified();
	}

	protected override void OnDelete( bool shift )
	{
		if ( TimeSelection is not { } selection ) return;

		var changed = false;

		using ( PushTrackModification( shift ? "Delete" : "Clear" ) )
		{
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

		using ( PushTrackModification( "Insert" ) )
		{
			foreach ( var track in Session.EditableTracks )
			{
				changed |= track.Insert( selection.PeakTimeRange ) && Session.TrackModified( track );
			}
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
		Delete( false );

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
		if ( Clipboard is not { } clipboard ) return false;

		ClearChanges();

		var selection = clipboard.Selection + Session.CurrentPointer;
		var pasteTime = selection.TotalStart;

		TimeSelection = selection;
		ChangeOffset = pasteTime;

		var changed = false;

		ModificationOptions = new BlendModificationOptions( IsAdditive, ChangeOffset );

		foreach ( var (id, blocks) in clipboard.Tracks )
		{
			if ( blocks.Count == 0 ) continue;
			if ( Project.GetTrack( id ) is not IProjectPropertyTrack track ) continue;

			var state = GetOrCreateTrackModificationPreview( track );

			state.Modification = blocks.Select( x => x.Shift( -clipboard.Selection.TotalStart ) ).AsModification();
			state.Update( selection, ModificationOptions );

			changed = true;
		}

		_changeDuration = changed ? clipboard.Selection.TotalTimeRange.Duration : null;

		SelectionChanged();

		return changed;
	}

	private ITrackModificationPreview? GetTrackModificationPreview( IProjectPropertyTrack track )
	{
		return TrackModificationPreviews!.GetValueOrDefault( track );
	}

	private ITrackModificationPreview GetOrCreateTrackModificationPreview( IProjectPropertyTrack track )
	{
		if ( GetTrackModificationPreview( track ) is { } state ) return state;

		var type = typeof(TrackModificationPreview<>).MakeGenericType( track.TargetType );
		TrackModificationPreviews.Add( track, state = (ITrackModificationPreview)Activator.CreateInstance( type, this, track )! );

		return state;
	}

	protected override void OnTrackStateChanged( DopeSheetTrack track )
	{
		if ( TimeSelection is not { } selection || ModificationOptions is not { } options ) return;

		if ( GetTrackModificationPreview( track.ProjectTrack ) is { } state )
		{
			state.Update( selection, options );
		}
	}

	protected override bool OnPreChange( DopeSheetTrack track )
	{
		if ( TimeSelection is null ) return false;
		if ( track.TrackWidget.Target is not { } property )
		{
			return false;
		}

		if ( TrackModificationPreviews.ContainsKey( track.ProjectTrack ) )
		{
			return false;
		}

		// We create modifications in PreChange so we can capture the pre-change value,
		// used for additive blending

		var preview = GetOrCreateTrackModificationPreview( track.ProjectTrack );

		preview.Modification = property.Value.AsSignal( property.TargetType ).AsModification();

		return true;
	}

	protected override bool OnPostChange( DopeSheetTrack track )
	{
		if ( TimeSelection is not { } selection || ModificationOptions is not { } options ) return false;

		if ( track.TrackWidget.Target is not { } property )
		{
			return false;
		}

		if ( GetTrackModificationPreview( track.ProjectTrack ) is not { Modification: ISignalBlendModification blend } preview )
		{
			return false;
		}

		ModificationOptions = new BlendModificationOptions( IsAdditive, ChangeOffset );

		preview.Modification = blend.WithSignal( property.Value.AsSignal( property.TargetType ) );

		return preview.Update( selection, options );
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

			if ( ModificationOptions is { } options )
			{
				foreach ( var (_, state) in TrackModificationPreviews )
				{
					state.Update( selection, options );
				}
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
