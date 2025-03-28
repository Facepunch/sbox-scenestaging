using System.Collections.Immutable;
using System.Linq;
using Sandbox;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

partial class MotionEditMode
{
	private IMovieModification? _modification;
	private ToolbarGroup? _modificationControls;

	private RealTimeSince _lastActionTime;

	public override bool AllowTrackCreation => TimeSelection is not null;

	public IMovieModification? Modification
	{
		get => _modification;
	}

	public bool HasChanges => Modification is not null;

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

	public IMovieModification SetModification( Type type, TimeSelection selection )
	{
		if ( _modification?.GetType() == type )
		{
			TimeSelection = selection;
			return _modification;
		}

		_modification?.ClearPreview();
		_modificationControls?.Destroy();

		_modification = (IMovieModification)Activator.CreateInstance( type )!;
		_modification.Initialize( this );

		_modificationControls = Toolbar.AddGroup();

		_modification.AddControls( _modificationControls );

		var commit = _modificationControls.AddAction( "Apply", "done", CommitChanges );
		var cancel = _modificationControls.AddAction( "Cancel", "clear", ClearChanges );

		commit.Background = Theme.Green;
		cancel.Background = Theme.Red;

		TimeSelection = selection;

		return _modification;
	}

	public T SetModification<T>( TimeSelection selection ) where T : IMovieModification =>
		(T)SetModification( typeof(T), selection );

	private void ClearChanges()
	{
		if ( Modification is null ) return;

		Modification?.ClearPreview();

		_modificationControls?.Destroy();
		_modificationControls = null;

		_modification = null;

		DisplayAction( "clear" );
		SelectionChanged();
	}

	private void CommitChanges()
	{
		if ( TimeSelection is not { } selection || Modification is not { } modification || !HasChanges ) return;

		using ( PushTrackModification( "Commit", true ) )
		{
			modification.Commit( selection );
		}

		ClearChanges();
		DisplayAction( "approval" );

		Session.ClipModified();
	}

	private void Delete( bool shiftTime )
	{
		if ( TimeSelection is not { } selection ) return;

		var changed = false;

		using ( PushTrackModification( shiftTime ? "Remove Time" : "Clear Time" ) )
		{
			foreach ( var view in Session.TrackList.EditableTracks )
			{
				var track = (IProjectPropertyTrack)view.Track;

				if ( shiftTime )
				{
					changed |= track.Remove( selection.PeakTimeRange ) && view.NoteInteraction();
				}
				else
				{
					changed |= track.Clear( selection.PeakTimeRange ) && view.NoteInteraction();
				}
			}
		}

		if ( changed )
		{
			Session.ClipModified();
			DisplayAction( "delete" );
		}
	}

	protected override void OnBackspace()
	{
		Delete( true );
	}

	protected override void OnDelete()
	{
		Delete( false );
	}

	protected override void OnInsert()
	{
		if ( TimeSelection is not { } selection ) return;

		var changed = false;

		using ( PushTrackModification( "Insert" ) )
		{
			foreach ( var view in Session.TrackList.EditableTracks )
			{
				var track = (IProjectPropertyTrack)view.Track;

				changed |= track.Insert( selection.PeakTimeRange ) && view.NoteInteraction();
			}
		}

		if ( changed )
		{
			DisplayAction( "keyboard_tab" );
		}
	}

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

		foreach ( var view in Session.TrackList.EditableTracks )
		{
			var track = (IProjectPropertyTrack)view.Track;

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

		SetModification<BlendModification>( selection )
			.SetFromClipboard( clipboard, pasteTime, Project );

		SelectionChanged();

		return true;
	}

	protected override void OnTrackStateChanged( ITrackView view )
	{
		if ( view.Track is not IProjectPropertyTrack track ) return;
		if ( TimeSelection is not { } selection || Modification is not { } modification ) return;

		modification.UpdatePreview( selection, track );
	}

	protected override bool OnPreChange( ITrackView view )
	{
		if ( TimeSelection is not { } selection ) return false;
		if ( view.Track is not IProjectPropertyTrack track ) return false;
		if ( view.Target is not ITrackProperty property ) return false;

		if ( Modification is not BlendModification blend )
		{
			Modification?.Commit( selection );

			blend = SetModification<BlendModification>( selection );
		}

		return blend.PreChange( track, property );
	}

	protected override bool OnPostChange( ITrackView view )
	{
		if ( TimeSelection is not { } selection || Modification is not BlendModification blend ) return false;
		if ( view.Track is not IProjectPropertyTrack track ) return false;
		if ( view.Target is not ITrackProperty property ) return false;

		return blend.PostChange( track, property ) && blend.UpdatePreview( selection, track );
	}

	private bool _hasSelectionItems;

	private void SelectionChanged()
	{
		if ( TimeSelection is { } selection )
		{
			SourceTimeRange = Modification?.SourceTimeRange;

			Modification?.UpdatePreview( selection );

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

			SourceTimeRange = null;

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
