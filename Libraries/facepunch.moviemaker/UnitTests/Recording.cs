using System;
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
	public void Clip()
	{
		var rootTrack = MovieClip.RootGameObject( "Example" );

		// Create an object we want to record

		var gameObject = new GameObject( true, "Example" );

		// Create a recorder

		var recorder = new MovieClipRecorder( Game.ActiveScene, RecorderOptions.Default )
		{
			Tracks =
			{
				rootTrack.Property<bool>( nameof(GameObject.Enabled) ),
				rootTrack.Property<Vector3>( nameof(GameObject.LocalPosition) ),
				rootTrack.Property<Rotation>( nameof(GameObject.LocalRotation) )
			},

			Binder =
			{
				{ rootTrack, gameObject }
			}
		};

		// Simulate a scene

		for ( var time = 0f; time < 10f; )
		{
			var deltaTime = Random.Shared.Float( 0.01f, 0.1f );

			time += deltaTime;

			AnimateObject( gameObject, time );

			// Advance the recorder by deltaTime, it will capture samples as
			// needed to match its sample rate

			recorder.Advance( deltaTime );
		}

		// Compile to a MovieClip that you can write to disk / play back in a MoviePlayer

		var clip = recorder.ToClip();

		Log.Info( Json.Serialize( clip ) );
	}
}
