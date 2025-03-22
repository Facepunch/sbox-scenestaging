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
		_stopPlayingAfterRecording = !Session.IsPlaying;
		_recordingStartTime = Session.CurrentPointer;
		_recordingLastTime = _recordingStartTime;

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

		var timeRange = new MovieTimeRange( 0d, recorder.Duration );

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

		Clipboard = new ClipboardData( new TimeSelection( timeRange, DefaultInterpolation ), compiled.Tracks
			.OfType<IPropertyTrack>()
			.Select( Project.GetTrack )
			.OfType<IProjectPropertyTrack>()
			.ToImmutableDictionary( x => x.Id, x => x.CreateSourceBlocks( sourceClip ) ) );

		Session.SetCurrentPointer( _recordingStartTime );

		if ( LoadChangesFromClipboard() )
		{
			DisplayAction( "radio_button_checked" );
		}
	}

	private void RecordingFrame()
	{
		if ( !Session.IsRecording ) return;

		var time = Session.CurrentPointer;
		var deltaTime = MovieTime.Max( time - _recordingLastTime, 0d );

		if ( _recorder?.Advance( deltaTime ) is true )
		{
			foreach ( var trackRecorder in _recorder.Tracks )
			{
				var track = (IProjectPropertyTrack)trackRecorder.Track;
				var finishedBlocks = trackRecorder.FinishedBlocks;

				if ( trackRecorder.CurrentBlock is { } current )
				{
					SetPreviewBlocks( track, [..finishedBlocks, current], _recordingStartTime );
				}
				else
				{
					SetPreviewBlocks( track, finishedBlocks, _recordingStartTime );
				}
			}
		}

		_recordingLastTime = time;
	}
}
