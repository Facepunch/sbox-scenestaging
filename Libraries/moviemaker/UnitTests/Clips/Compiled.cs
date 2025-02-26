using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sandbox.MovieMaker.Compiled;

namespace Sandbox.MovieMaker.Test;

[TestClass]
public sealed class Compiled
{
	[TestMethod]
	public void Serialize()
	{
		var cameraTrack = new CompiledTrack(
			Id: Guid.NewGuid(),
			Name: "Camera",
			TargetType: typeof(CameraComponent) );

		var cameraPosTrack = new CompiledTrack(
			Id: Guid.NewGuid(),
			Name: nameof(GameObject.LocalPosition),
			TargetType: typeof(Vector3),
			Parent: cameraTrack,
			new SampleBlock<Vector3>(
				TimeRange: (MovieTime.Zero, MovieTime.FromSeconds( 1d )),
				Offset: MovieTime.Zero,
				SampleRate: 1,
				new Vector3( 100f, 0f, 200f ),
				new Vector3( 500f, 300f, 200f ) ) );

		var clip = new CompiledClip( cameraTrack, cameraPosTrack );

		if ( cameraPosTrack.TryGetValue( MovieTime.FromSeconds( 0.5 ), out Vector3 value ) )
		{
			Console.WriteLine( value );
		}
	}
}
