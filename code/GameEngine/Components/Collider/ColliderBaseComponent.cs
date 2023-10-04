using Sandbox;
using Sandbox.Diagnostics;

public abstract class ColliderBaseComponent : GameObjectComponent
{
	PhysicsShape shape;
	protected PhysicsBody ownBody;

	public override void OnEnabled()
	{
		Assert.IsNull( ownBody );
		Assert.IsNull( shape );

		PhysicsBody physicsBody = null;

		// is there a physics body?
		var body = GameObject.GetComponentInParent<PhysicsComponent>();
		if ( body is not null )
		{
			physicsBody = body.GetBody();

			//
			if ( physicsBody is null )
			{
				Log.Warning( $"{this}: PhysicsBody from {body} was null" );
				return;
			}

		}
		
		if ( physicsBody is null )
		{
			physicsBody = new PhysicsBody( Scene.PhysicsWorld );
			physicsBody.BodyType = PhysicsBodyType.Keyframed;
			physicsBody.GameObject = GameObject;
			physicsBody.Transform = GameObject.WorldTransform;
			physicsBody.UseController = true;
			physicsBody.GravityEnabled = false;
			ownBody = physicsBody;
		}

		shape = CreatePhysicsShape( physicsBody );
	}

	protected abstract PhysicsShape CreatePhysicsShape( PhysicsBody targetBody );

	public override void OnDisabled()
	{
		//shape?.Body?.RemoveShape( shape );
		shape?.Remove();
		shape = null;

		ownBody?.Remove();
		ownBody = null;
	}

	protected override void OnPostPhysics()
	{
		ownBody?.Move( GameObject.WorldTransform, Time.Delta * 4.0f );
	}

	public void OnPhysicsChanged()
	{
		Log.Info( "Physics Changed" );
		OnDisabled();
		OnEnabled();
	}
}
