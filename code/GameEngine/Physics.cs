using Sandbox;

public static class Physics
{
	public static PhysicsTraceBuilder Trace => GameManager.ActiveScene.PhysicsWorld.Trace;
}
