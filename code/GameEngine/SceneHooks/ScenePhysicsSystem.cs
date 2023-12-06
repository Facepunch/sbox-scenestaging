namespace Sandbox;

/// <summary>
/// Ticks the physics in FrameStage.PhysicsStep
/// </summary>
class ScenePhysicsSystem : GameObjectSystem
{
	public ScenePhysicsSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.PhysicsStep, 0, UpdatePhysics, "UpdatePhysics" );
	}

	void UpdatePhysics()
	{
		var idealHz = 120.0f;
		var idealStep = 1.0f / idealHz;
		int steps = (Time.Delta / idealStep).FloorToInt().Clamp( 1, 10 );

		using ( Sandbox.Utility.Superluminal.Scope( "PhysicsWorld.Step", Color.Cyan ) )
		{
			Scene.PhysicsWorld.Step( Time.Now, Time.Delta, steps );
		}
	}
}
