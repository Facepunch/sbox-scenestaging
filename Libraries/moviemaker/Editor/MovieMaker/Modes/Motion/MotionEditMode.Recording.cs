using System.Collections.Immutable;
using Sandbox.MovieMaker;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Linq;

namespace Editor.MovieMaker;

#nullable enable

partial class MotionEditMode
{
	private bool _stopPlayingAfterRecording;

	public override bool AllowRecording => true;

	private MovieClipRecorder? _recorder;
	private MovieTime _recordingStartTime;
	private MovieTime _recordingLastTime;

	protected override bool OnStartRecording()
	{
		var options = new RecorderOptions( Project.SampleRate );

		_recorder = new MovieClipRecorder( Session.Binder, options );
		_recordingStartTime = Session.CurrentPointer;
		_stopPlayingAfterRecording = !Session.IsPlaying;

		foreach ( var track in Session.EditableTracks )
		{
			_recorder.Tracks.Add( track );
		}

		Session.IsPlaying = true;

		return true;
	}

	protected override void OnStopRecording()
	{
		if ( _recorder is not { } recorder ) return;

		if ( _stopPlayingAfterRecording )
		{
			Session.IsPlaying = false;
		}

		foreach ( var trackRecorder in recorder.Tracks )
		{
			ClearPreviewBlocks( (IProjectPropertyTrack)trackRecorder.Track );
		}

		var compiled = recorder.ToClip();

		var sourceClip = Project.AddSourceClip( compiled, new JsonObject
		{
			{ "Date", DateTime.UtcNow.ToString( "o", CultureInfo.InvariantCulture ) },
			{ "IsEditor", Session.Player.Scene.IsEditor },
			{ "SceneSource", Json.ToNode( Session.Player.Scene.Source ) },
			{ "MoviePlayer", Json.ToNode( Session.Player.Id ) }
		} );

		var range = new MovieTimeRange( _recordingStartTime, _recordingStartTime + compiled.Duration );

		Clipboard = new ClipboardData( new TimeSelection( range, DefaultInterpolation ), compiled.Tracks
			.OfType<IPropertyTrack>()
			.Select( x => Project.GetTrack( x ) )
			.OfType<IProjectPropertyTrack>()
			.ToImmutableDictionary( x => x.Id, x => x.CreateSourceBlocks( sourceClip ) ) );

		Session.SetCurrentPointer( range.Start );

		if ( LoadChangesFromClipboard() )
		{
			DisplayAction( "radio_button_checked" );
		}
	}

	private void RecordingFrame()
	{
		if ( !Session.IsRecording ) return;

		var time = Session.CurrentPointer;

		if ( _recorder?.Update( MovieTime.Max( time - _recordingLastTime, 0d ) ) is true )
		{
			foreach ( var trackRecorder in _recorder.Tracks )
			{
				var track = (IProjectPropertyTrack)trackRecorder.Track;

				if ( trackRecorder.CurrentBlock is { } current )
				{
					SetPreviewBlocks( track, [..trackRecorder.FinishedBlocks, current] );
				}
				else
				{
					SetPreviewBlocks( track, trackRecorder.FinishedBlocks );
				}
			}
		}

		_recordingLastTime = time;
	}
}
