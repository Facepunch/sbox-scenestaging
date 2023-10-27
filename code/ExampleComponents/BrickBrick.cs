using Sandbox;
using System;

public sealed class BrickBrick : BaseComponent, BaseComponent.ICollisionListener
{
	public override void OnEnabled()
	{
		base.OnEnabled();

	//	Transform.LocalRotation *= Rotation.From( Random.Shared.Float( -15, 15 ), Random.Shared.Float( -15, 15 ), 0 );
	}

	public override void Update()
	{
		
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
