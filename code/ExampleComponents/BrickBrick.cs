using Sandbox;
using System;

public sealed class BrickBrick : Component, Component.ICollisionListener
{
	protected override void OnEnabled()
	{
		base.OnEnabled();

	//	Transform.LocalRotation *= Rotation.From( Random.Shared.Float( -15, 15 ), Random.Shared.Float( -15, 15 ), 0 );
	}

	public void OnCollisionStart( Collision o )
	{
		if ( o.Other.GameObject.Tags.Has( "ball" ) )
		{
			GameObject.Destroy();
		}
	}

	public void OnCollisionStop( CollisionStop other )
	{
		
	}

	public void OnCollisionUpdate( Collision other )
	{
		
	}
}
