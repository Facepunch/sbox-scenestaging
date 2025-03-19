using System;
using Sandbox.MovieMaker.Compiled;

namespace Sandbox.MovieMaker.Test;

[TestClass]
public sealed class CompiledTests
{
	public IClip CreateExampleClip()
	{
		var rootTrack = CompiledClip.RootGameObject( "Camera" );
		var cameraTrack = rootTrack.Component<CameraComponent>();

		return CompiledClip.FromTracks( rootTrack, cameraTrack,
			rootTrack.Property<Vector3>( nameof(GameObject.LocalPosition) )
				.WithConstant( (0f, 2f), new Vector3( 100f, 200f, 300f ) ),
			cameraTrack.Property<float>( nameof(CameraComponent.FieldOfView) )
				.WithSamples( (1f, 3f), sampleRate: 2, [60f, 75f, 65f, 90f, 50f] ) );
	}

	public IClip RoundTripSerialize( IClip clip )
	{
		return Json.Deserialize<CompiledClip>( Json.Serialize( clip ) );
	}

	[TestMethod]
	public void Serialize()
	{
		var clip = CreateExampleClip();
		var json = Json.Serialize( clip );

		Console.WriteLine( json );

		clip = Json.Deserialize<CompiledClip>( json );

		Assert.AreEqual( 3d, clip.Duration.TotalSeconds );

		var cameraPosTrack = clip.GetProperty<Vector3>( "Camera", nameof(GameObject.LocalPosition) );
		var fovTrack = clip.GetProperty<float>( "Camera", nameof(CameraComponent), nameof(CameraComponent.FieldOfView) );

		Assert.IsNotNull( cameraPosTrack );

		Assert.IsTrue( cameraPosTrack.TryGetValue( 1.5, out var position ) );
		Assert.IsFalse( cameraPosTrack.TryGetValue( 2.5, out _ ) );

		Assert.AreEqual( new Vector3( 100f, 200f, 300f ), position );

		Assert.IsNotNull( fovTrack );

		Assert.IsTrue( fovTrack.TryGetValue( 1.25, out var fov ) );
		Assert.IsFalse( fovTrack.TryGetValue( 0.5, out _ ) );

		Assert.AreEqual( (60f + 75f) / 2f, fov );
	}
}
