using Sandbox;

public sealed class WaterPlayer : Component, Component.ICollisionListener
{

	ModelViewerPlayerController playerController;


	public void OnCollisionStart( Collision other )
	{
		if ( other.Other.GameObject.Tags.Has( "player" ) )
		{
			playerController = other.Other.GameObject.Parent.Components.Get<ModelViewerPlayerController>(FindMode.EnabledInSelfAndDescendants);
			playerController.IsSwiming = true;
		}
	}

	public void OnCollisionUpdate( Collision other )
	{

	}

	public void OnCollisionStop( CollisionStop other )
	{
		playerController.IsSwiming = false;
		playerController = null;
	}

	public void OnTriggerEnter( Collider other )
	{

	}

	public void OnTriggerExit( Collider other )
	{
	}
}
