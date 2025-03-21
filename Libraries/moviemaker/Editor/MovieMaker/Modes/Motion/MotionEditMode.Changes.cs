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

	public ModificationOptions? ModificationOptions
	{
		get => TimeSelection is { } selection
				? new ModificationOptions( selection, ChangeOffset, IsAdditive, SmoothingSteps, SmoothingSize )
				: null;

		set
		{
			if ( value is not { } options )
			{
				TimeSelection = null;
				return;
			}

			_timeSelection = options.Selection;
			_additive = options.Additive;
			_smoothSteps = options.SmoothSteps;
			ChangeOffset = options.Offset;

			SelectionChanged();
		}
	}

	public bool HasChanges => TrackModifications.Values.Any( x => x.HasChanges );

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

	private Dictionary<IProjectPropertyTrack, ITrackModification> TrackModifications { get; } = new();

	private void ClearChanges()
	{
		if ( !HasChanges ) return;

		foreach ( var state in TrackModifications.Values )
		{
			state.ClearPreview();
		}

		_changeDuration = null;

		TrackModifications.Clear();

		DisplayAction( "clear" );
	}

	private void CommitChanges()
	{
		if ( ModificationOptions is not { } options || !HasChanges ) return;

		using ( PushTrackModification( "Commit", true ) )
		{
			foreach ( var (_, state) in TrackModifications )
			{
				state.Commit( options );
			}

			TrackModifications.Clear();
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

		foreach ( var (id, blocks) in clipboard.Tracks )
		{
			if ( blocks.Count == 0 ) continue;
			if ( Project.GetTrack( id ) is not IProjectPropertyTrack track ) continue;

			var state = GetOrCreateTrackModification( track );

			state.SetClipboardOverlay( blocks.Select( x => x.Shift( -clipboard.Selection.TotalStart ) ) );
			state.Update( ModificationOptions!.Value );

			changed = true;
		}

		_changeDuration = changed ? clipboard.Selection.TotalTimeRange.Duration : null;

		SelectionChanged();

		return changed;
	}

	private ITrackModification? GetTrackModification( IProjectPropertyTrack track )
	{
		return TrackModifications!.GetValueOrDefault( track );
	}

	private ITrackModification GetOrCreateTrackModification( IProjectPropertyTrack track )
	{
		if ( GetTrackModification( track ) is { } state ) return state;

		var type = typeof(TrackModification<>).MakeGenericType( track.TargetType );
		TrackModifications.Add( track, state = (ITrackModification)Activator.CreateInstance( type, this, track )! );

		return state;
	}

	protected override void OnTrackStateChanged( DopeSheetTrack track )
	{
		if ( ModificationOptions is not { } options ) return;

		if ( GetTrackModification( track.ProjectTrack ) is { } state )
		{
			state.Update( options );
		}
	}

	protected override bool OnPreChange( DopeSheetTrack track )
	{
		if ( TimeSelection is not { } selection ) return false;
		if ( track.TrackWidget.Target is not { } property )
		{
			return false;
		}

		if ( TrackModifications.ContainsKey( track.ProjectTrack ) )
		{
			return false;
		}

		// We create modifications in PreChange so we can capture the pre-change value,
		// used for additive blending

		var modification = GetOrCreateTrackModification( track.ProjectTrack );

		modification.SetRelativeTo( property.Value );

		return true;
	}

	protected override bool OnPostChange( DopeSheetTrack track )
	{
		if ( ModificationOptions is not { } options ) return false;

		if ( track.TrackWidget.Target is not { } property )
		{
			return false;
		}

		if ( GetTrackModification( track.ProjectTrack ) is not { } state )
		{
			return false;
		}

		state.SetConstantOverlay( property.Value );

		return state.Update( options );
	}

	private bool _hasSelectionItems;

	private void SelectionChanged()
	{
		if ( _smoothSlider is { } slider )
		{
			slider.Slider.Hidden = !SmoothingEnabled;
			slider.Label.Hidden = !SmoothingEnabled;
		}

		if ( ModificationOptions is { } options )
		{
			PasteTimeRange = _changeDuration is { } duration ? (ChangeOffset, ChangeOffset + duration) : null;

			foreach ( var (_, state) in TrackModifications )
			{
				state.Update( options );
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
