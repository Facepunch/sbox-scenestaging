using Sandbox;
using Sandbox.Diagnostics;

[Title( "Collider - Map" )]
[Category( "Physics" )]
[Icon( "panorama_fish_eye", "red", "white" )]
public class ColliderMapComponent : ColliderBaseComponent
{
	public ColliderMapComponent()
	{

	}

	protected override PhysicsShape CreatePhysicsShape( PhysicsBody targetBody )
	{
		return null;
	}

	internal void SetBody( PhysicsBody body )
	{
		ownBody = body;
	}

	public override void OnEnabled()
	{
		// nothing
	}

	public override void OnDisabled()
	{
		// nothing
	}
}
