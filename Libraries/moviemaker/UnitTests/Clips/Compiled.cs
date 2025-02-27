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
		var timeRange = new MovieTimeRange( MovieTime.Zero, MovieTime.FromSeconds( 4 ) );

		var rootTrack = Track.GameObject( "Camera" );
		var cameraPosTrack = Track.Property<Vector3>( nameof(GameObject.LocalPosition), rootTrack )
			.WithConstant( timeRange, new Vector3( 100f, 200f, 300f ) );

		var cameraTrack = Track.Component<CameraComponent>( rootTrack );
		var fovTrack = Track.Property<float>( nameof(CameraComponent.FieldOfView), cameraTrack )
			.WithSamples( timeRange, sampleRate: 1, [60f, 75f, 65f, 90f, 50f] );

		var clip = new Clip( rootTrack, cameraTrack, cameraPosTrack, fovTrack );

		for ( var t = timeRange.Start; t <= timeRange.End; t += MovieTime.FromSeconds( 0.25 ) )
		{
			if ( cameraPosTrack.TryGetValue( MovieTime.FromSeconds( 0.5 ), out Vector3 value ) )
			{
				Console.WriteLine( $"{t}: {value}" );
			}
		}
	}
}
