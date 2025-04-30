namespace Sandbox.MovieMaker.Controllers;

#nullable enable

internal class PlayerControllerDirector : IComponentDirector<PlayerController>
{
	public required PlayerController Component { get; init; }

	IEnumerable<Component> IComponentDirector.AutoDirectedComponents
	{
		get
		{
			if ( Component.Renderer.IsValid() ) yield return Component.Renderer;
			if ( Component.Body.IsValid() ) yield return Component.Body;
		}
	}

	void IComponentDirector.Update( MoviePlayer player, MovieTime deltaTime )
	{
		if ( !player.Scene.IsEditor && player.IsPlaying ) return;

		if ( player.Scene.PhysicsWorld.IsValid() )
		{
			((IScenePhysicsEvents)Component).PrePhysicsStep();
			((IScenePhysicsEvents)Component).PostPhysicsStep();
		}

		if ( Component.Renderer is { IsValid: true } renderer )
		{
			Component.UpdateAnimation( renderer );
		}
	}
}
