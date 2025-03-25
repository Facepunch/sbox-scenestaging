using System.Collections.Immutable;
using System.Linq;
using Sandbox.MovieMaker;
using Sandbox.Utility;

namespace Editor.MovieMaker;

#nullable enable
partial class MotionEditMode : EditMode
{
	public EditModeHistory<MotionEditMode> History { get; }

	public IDisposable PushTrackModification( string title, bool includeClipboard = false )
	{
		var before = Snapshot();

		return new DisposeAction( () =>
		{
			var after = Snapshot();

			History.Push( $"Changed Tracks", editMode =>
			{
				editMode.Restore( after );
				DisplayAction( "redo" );
			}, editMode =>
			{
				editMode.Restore( before );
				DisplayAction( "undo" );
			} );
		} );
	}

	private record PropertyTrackSnapshot( ImmutableArray<IProjectPropertyBlock> Blocks );
	private record ProjectSnapshot( ImmutableDictionary<Guid, PropertyTrackSnapshot> Tracks, int SampleRate );

	private record SessionSnapshot( ProjectSnapshot? Project, TimeSelection? Selection, ModificationSnapshot? Modification,
		MovieTime TimeOffset, float PixelsPerSecond, int FrameRate );

	private SessionSnapshot Snapshot()
	{
		var projectSnapshot = new ProjectSnapshot(
			Project.Tracks.OfType<IProjectPropertyTrack>().ToImmutableDictionary(
				x => x.Id,
				x => new PropertyTrackSnapshot( [..x.Blocks] ) ),
			Project.SampleRate );

		return new SessionSnapshot( projectSnapshot, TimeSelection, Modification?.Snapshot(),
			Session.TimeOffset, Session.PixelsPerSecond, Session.FrameRate );
	}

	private void Restore( SessionSnapshot snapshot )
	{
		if ( snapshot.Project is { } projectSnapshot )
		{
			foreach ( var (guid, trackSnapshot) in projectSnapshot.Tracks )
			{
				if ( Project.GetTrack( guid ) is not IProjectPropertyTrack propertyTrack ) continue;

				propertyTrack.SetBlocks( trackSnapshot.Blocks );

				Session.TrackList.Find( propertyTrack )?.NoteInteraction();
			}
		}

		ClearChanges();

		if ( snapshot is { Selection: { } selection, Modification: { } modificationSnapshot } )
		{
			_timeSelection = selection;

			SetModification( modificationSnapshot.Type, selection )
				.Restore( modificationSnapshot );
		}

		Session.SetView( snapshot.TimeOffset, snapshot.PixelsPerSecond );
		SelectionChanged();
	}
}
