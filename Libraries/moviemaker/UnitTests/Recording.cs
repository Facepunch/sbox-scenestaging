using System;
using System.Text.Json;
using Sandbox.MovieMaker.Compiled;

namespace Sandbox.MovieMaker.Test;

#nullable enable

[TestClass]
public sealed class RecordingTests : SceneTests
{
	private static void AnimateObject( GameObject obj, float time )
	{
		obj.Enabled = time.UnsignedMod( 3f ) <= 2f;

		obj.WorldPosition = Vector3.Lerp(
			new Vector3( -100f, 0f, 0f ),
			new Vector3( 100f, 0f, 0f ),
			MathF.Sin( time * MathF.PI ) );

		obj.WorldRotation = Rotation.FromYaw( time * 90f );
	}

	[TestMethod]
	public void SingleTrack()
	{
		var track = MovieClip.RootGameObject( "Example" )
			.Property<Vector3>( nameof(GameObject.LocalPosition) );

		var recorder = track.CreateRecorder();

		var gameObject = new GameObject( true, "Example" );

		var deltaTime = 1f / 60f;

		for ( var time = 0f; time < 10f; time += deltaTime )
		{
			AnimateObject( gameObject, time );

			recorder.Update( deltaTime );
		}

		track = track with { Blocks = recorder.ToBlocks() };

		Log.Info( Json.Serialize( MovieClip.FromTracks( track ) ) );
	}

	[TestMethod]
	public void Clip()
	{
		// Create some tracks to record

		var rootTrack = MovieClip.RootGameObject( "Example" );

		var tracks = new ITrack[]
		{
			rootTrack.Property<bool>( nameof(GameObject.Enabled) ),
			rootTrack.Property<Vector3>( nameof(GameObject.LocalPosition) ),
			rootTrack.Property<Rotation>( nameof(GameObject.LocalRotation) )
		};

		// Create an object we want to record

		var gameObject = new GameObject( true, "Example" );

		// Optionally create a binder to manually set what objects in the
		// scene get recorded to which tracks

		var binder = new TrackBinder();

		binder.Get( rootTrack ).Bind( gameObject );

		// Create a recorder for those tracks.

		var recorder = new MovieClipRecorder( tracks, binder,
			options: new RecorderOptions( SampleRate: 30 ) );

		// Simulate a scene

		for ( var time = 0f; time < 10f; )
		{
			var deltaTime = Random.Shared.Float( 0.01f, 0.1f );

			time += deltaTime;

			AnimateObject( gameObject, time );

			// Advance the recorder by deltaTime, it will capture samples as
			// needed to match its sample rate

			recorder.Update( deltaTime );
		}

		// Compile to a MovieClip that you can write to disk / play back in a MoviePlayer

		var clip = recorder.ToClip();

		Log.Info( Json.Serialize( clip ) );
	}
}
