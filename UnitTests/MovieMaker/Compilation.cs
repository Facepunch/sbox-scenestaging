using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sandbox.MovieMaker.Compiled;

namespace Editor.MovieMaker.Test;

[TestClass]
public class Compilation
{
	/// <summary>
	/// Test constant signal compilation.
	/// </summary>
	[TestMethod]
	public void Constant()
	{
		var block = new PropertyBlock<float>( 1f, (0, 10) );

		var exampleTrack = MovieClip.RootGameObject( "Example" )
			.Property( "Property", block.Compile() );

		Assert.AreEqual( 1, exampleTrack.Blocks.Length );
		Assert.IsInstanceOfType<CompiledConstantBlock<float>>( exampleTrack.Blocks[0] );

		// Compiled block must match start / end time of source block

		Assert.AreEqual( 0d, exampleTrack.Blocks[0].TimeRange.Start );
		Assert.AreEqual( 10d, exampleTrack.Blocks[0].TimeRange.End );
	}

	/// <summary>
	/// Test constant signal compilation, when not aligned to sample rate.
	/// </summary>
	[TestMethod]
	public void ConstantMisaligned()
	{
		var block = new PropertyBlock<float>( 1f, (0.01, 9.99) );

		var exampleTrack = MovieClip.RootGameObject( "Example" )
			.Property( "Property", block.Compile() );

		Assert.AreEqual( 1, exampleTrack.Blocks.Length );
		Assert.IsInstanceOfType<CompiledConstantBlock<float>>( exampleTrack.Blocks[0] );

		// Compiled block must match start / end time of source block

		Assert.AreEqual( 0.01, exampleTrack.Blocks[0].TimeRange.Start );
		Assert.AreEqual( 9.99, exampleTrack.Blocks[0].TimeRange.End );
	}

	private static PropertySignal<float> CreateKeyframeSignal() => PropertySignal.FromKeyframes( [
		new Keyframe<float>( 0, 0f, KeyframeInterpolation.Linear ),
		new Keyframe<float>( 5, 0f, KeyframeInterpolation.Linear ),
		new Keyframe<float>( 10, 1f, KeyframeInterpolation.Cubic )
	] );

	/// <summary>
	/// Test keyframe signal compilation, where the signal can be represented as a constant block
	/// followed by a sample block.
	/// </summary>
	[TestMethod]
	public void Keyframes()
	{
		var block = new PropertyBlock<float>( CreateKeyframeSignal(), (0, 10) );

		var exampleTrack = MovieClip.RootGameObject( "Example" )
			.Property( "Property", block.Compile() );

		// Signal is constant, followed by a changing value

		Assert.AreEqual( 2, exampleTrack.Blocks.Length );
		Assert.IsInstanceOfType<CompiledConstantBlock<float>>( exampleTrack.Blocks[0] );
		Assert.IsInstanceOfType<CompiledSampleBlock<float>>( exampleTrack.Blocks[1] );

		// Compiled blocks must be adjacent, and match start / end time of source block

		Assert.AreEqual( 0, exampleTrack.Blocks[0].TimeRange.Start );
		Assert.AreEqual( exampleTrack.Blocks[0].TimeRange.End, exampleTrack.Blocks[1].TimeRange.Start );
		Assert.AreEqual( 10, exampleTrack.Blocks[1].TimeRange.End );
	}

	/// <summary>
	/// Test constant signal compilation, when not aligned to sample rate.
	/// </summary>
	[TestMethod]
	public void KeyframesMisaligned()
	{
		var block = new PropertyBlock<float>( CreateKeyframeSignal(), (0.01, 9.99) );

		var exampleTrack = MovieClip.RootGameObject( "Example" )
			.Property( "Property", block.Compile() );

		// Signal is constant, followed by a changing value

		Assert.AreEqual( 2, exampleTrack.Blocks.Length );
		Assert.IsInstanceOfType<CompiledConstantBlock<float>>( exampleTrack.Blocks[0] );
		Assert.IsInstanceOfType<CompiledSampleBlock<float>>( exampleTrack.Blocks[1] );

		// Compiled blocks must be adjacent, and match start / end time of source block

		Assert.AreEqual( 0.01, exampleTrack.Blocks[0].TimeRange.Start );
		Assert.AreEqual( exampleTrack.Blocks[0].TimeRange.End, exampleTrack.Blocks[1].TimeRange.Start );
		Assert.AreEqual( 9.99, exampleTrack.Blocks[1].TimeRange.End );
	}
}
