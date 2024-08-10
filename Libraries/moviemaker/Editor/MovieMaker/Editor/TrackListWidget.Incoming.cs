using Sandbox.Diagnostics;
using Sandbox.MovieMaker;
using System.Linq;

namespace Editor.MovieMaker;


public partial class TrackListWidget : EditorEvent.ISceneEdited
{
	void EditorEvent.ISceneEdited.GameObjectEdited( GameObject go, string propertyName )
	{
		var lastProperty = propertyName.Split( '.' ).Last();

		if ( lastProperty == "LocalScale" || lastProperty == "LocalRotation" || lastProperty == "LocalPosition" )
		{
			// make sure the track exists for this property
			var targetTrack = Session.Clip.FindTrack( go, lastProperty );

			if ( targetTrack is null )
			{
				if ( !Session.KeyframeRecording ) return;

				targetTrack = Session.Clip.FindOrCreateTrack( go, lastProperty );
				OnTrackCreated( targetTrack );
			}

			// make sure the track widget exists for this track
			RebuildTracksIfNeeded();

			var trackwidget = FindTrack( targetTrack );
			Assert.NotNull( trackwidget, "Track should have been created" );
			trackwidget.AddKey( Session.CurrentPointer );
			trackwidget.Write();

			ScrollArea.MakeVisible( trackwidget );
			trackwidget.NoteInteraction();
		}
	}

	void EditorEvent.ISceneEdited.ComponentEdited( Component cmp, string propertyName )
	{
		var targetTrack = Session.Clip.FindTrack( cmp, propertyName );

		if ( targetTrack is null )
		{
			if ( !Session.KeyframeRecording ) return;

			targetTrack = Session.Clip.FindOrCreateTrack( cmp, propertyName );
			OnTrackCreated( targetTrack );
		}

		// make sure the track widget exists for this track
		RebuildTracksIfNeeded();

		var trackwidget = FindTrack( targetTrack );
		Assert.NotNull( trackwidget, "Track should have been created" );
		trackwidget.AddKey( Session.CurrentPointer );
		trackwidget.Write();

		ScrollArea.MakeVisible( trackwidget );
		trackwidget.NoteInteraction();
	}


	void OnTrackCreated( MovieTrack track )
	{
		//EditorUtility.PlayRawSound( "sounds/editor/create.wav" );
	}
}

