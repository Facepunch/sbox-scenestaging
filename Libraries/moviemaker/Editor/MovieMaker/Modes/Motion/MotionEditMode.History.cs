using System.Collections.Immutable;
using System.Linq;
using System.Threading.Channels;
using Editor.ShaderGraph.Nodes;
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

	private record MotionEditorSnapshot(
		TimeSelection? Selection,
		ITrackModificationOptions? Options,
		ImmutableDictionary<Guid, TrackModificationSnapshot> Modifications );

	private record SessionSnapshot( ProjectSnapshot? Project, MotionEditorSnapshot? MotionEditor, MovieTime TimeOffset, float PixelsPerSecond, int FrameRate );

	private SessionSnapshot Snapshot()
	{
		var projectSnapshot = new ProjectSnapshot( Session.EditableTracks.ToImmutableDictionary(
			x => x.Id,
			x => new PropertyTrackSnapshot( [..x.Blocks] ) ), Project.SampleRate);

		var editorSnapshot = new MotionEditorSnapshot(
			TimeSelection,
			ModificationOptions,
			TrackModificationPreviews.ToImmutableDictionary(
				x => x.Key.Id,
				x => x.Value.Snapshot() ) );

		return new SessionSnapshot( projectSnapshot, editorSnapshot, Session.TimeOffset, Session.PixelsPerSecond, Session.FrameRate );
	}

	private void Restore( SessionSnapshot snapshot )
	{
		if ( snapshot.Project is { } projectSnapshot )
		{
			foreach ( var (guid, trackSnapshot) in projectSnapshot.Tracks )
			{
				if ( Project.GetTrack( guid ) is not IProjectPropertyTrack propertyTrack ) continue;

				propertyTrack.SetBlocks( trackSnapshot.Blocks );
				Session.TrackModified( propertyTrack );
			}
		}

		if ( snapshot.MotionEditor is { } editorSnapshot )
		{
			ClearChanges();

			TimeSelection = editorSnapshot.Selection;
			ModificationOptions = editorSnapshot.Options;

			if ( TimeSelection is { } selection && ModificationOptions is { } options )
			{
				foreach ( var (guid, modificationSnapshot) in editorSnapshot.Modifications )
				{
					if ( Project.GetTrack( guid ) is not IProjectPropertyTrack propertyTrack ) continue;

					var modification = GetOrCreateTrackModificationPreview( propertyTrack );

					modification.Restore( modificationSnapshot );
					modification.Update( selection, options );
				}
			}
		}

		Session.SetView( snapshot.TimeOffset, snapshot.PixelsPerSecond );
		SelectionChanged();
	}
}
