using System;
using Sandbox.MovieMaker.Compiled;

namespace Sandbox.MovieMaker.Test;

#nullable enable

[TestClass]
public sealed class RecordingTests : SceneTests
{
	private static void AnimateObject( GameObject obj, float time )
	{
		obj.WorldPosition = Vector3.Lerp(
			new Vector3( -100f, 0f, 0f ),
			new Vector3( 100f, 0f, 0f ),
			MathF.Sin( time * MathF.PI ) );

		obj.WorldRotation = Rotation.FromYaw( time * 90f );
	}

	[TestMethod]
	public void SingleTrack()
	{
		var recorder = MovieClip.RootGameObject( "Example" )
			.Property<Vector3>( nameof(GameObject.LocalPosition) )
			.CreateRecorder();

		var gameObject = new GameObject( true, "Example" );

		var deltaTime = 1f / 60f;

		for ( var t = 0f; t < 10f; t += deltaTime )
		{
			gameObject.WorldPosition = Vector3.Lerp(
				new Vector3( -100f, 0f, 0f ),
				new Vector3( 100f, 0f, 0f ),
				MathF.Sin( t * MathF.PI ) );

			recorder.Update( deltaTime );
		}

		Log.Info( recorder.Compile() );
	}

	[TestMethod]
	public void Clip()
	{
		var gameObjectTrack = MovieClip.RootGameObject( "Example" );
		var positionTrack = gameObjectTrack.Property<Vector3>( nameof(GameObject.LocalPosition) );
		var rotationTrack = gameObjectTrack.Property<Rotation>( nameof(GameObject.LocalRotation) );
		var recorder = MovieClip.FromTracks( positionTrack, rotationTrack )
			.CreateRecorder();

		var gameObject = new GameObject( true, "Example" );

		var deltaTime = 1f / 60f;

		for ( var time = 0f; time < 10f; time += deltaTime )
		{
			AnimateObject( gameObject, time );

			recorder.Update( deltaTime );
		}

		Log.Info( recorder.Compile() );
	}
}
