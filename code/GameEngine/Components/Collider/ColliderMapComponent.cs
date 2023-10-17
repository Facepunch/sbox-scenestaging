using Sandbox;
using Sandbox.Diagnostics;
using System.Collections.Generic;

[Title( "Collider - Map" )]
[Category( "Physics" )]
[Icon( "panorama_fish_eye", "red", "white" )]
public class ColliderMapComponent : ColliderBaseComponent
{
	public ColliderMapComponent()
	{

	}

	protected override IEnumerable<PhysicsShape> CreatePhysicsShapes( PhysicsBody targetBody )
	{
		yield break;
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
