using System;

namespace Sandbox.MovieMaker.Controllers;

#nullable enable

internal class SkinnedModelRendererDirector : IComponentDirector<SkinnedModelRenderer>
{
	private float _basePlaybackRate;

	public required SkinnedModelRenderer Component { get; init; }

	void IComponentDirector.Start( MoviePlayer player )
	{
		_basePlaybackRate = Component.PlaybackRate;

		Component.SceneModel.PlaybackRate = 0f;
	}

	void IComponentDirector.Update( MoviePlayer player, MovieTime deltaTime )
	{
		// Negative deltas aren't supported :(

		if ( deltaTime <= 0d ) return;

		Component.SceneModel.PlaybackRate = _basePlaybackRate;
		Component.SceneModel.Update( Math.Min( (float)deltaTime.TotalSeconds, 1f ) );
		Component.SceneModel.PlaybackRate = 0f;
	}

	void IComponentDirector.Stop( MoviePlayer player )
	{
		Component.SceneModel.PlaybackRate = _basePlaybackRate;
	}
}
