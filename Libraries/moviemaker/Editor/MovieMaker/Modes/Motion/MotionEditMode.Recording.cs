using System.Collections.Immutable;
using Sandbox.MovieMaker;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Linq;
using Sandbox.MovieMaker.Compiled;

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

		Session.IsPlaying = true;

		// SetPreviewBlocks( propertyTrack, [recording] );
		throw new NotImplementedException();

		return true;
	}

	protected override void OnStopRecording()
	{
		if ( _recorder is not { } recorder ) return;

		if ( _stopPlayingAfterRecording )
		{
			Session.IsPlaying = false;
		}

		// ClearPreviewBlocks( track );
		throw new NotImplementedException();

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

		_recorder?.Update( MovieTime.Max( time - _recordingLastTime, 0d ) );
		_recordingLastTime = time;
	}
}
