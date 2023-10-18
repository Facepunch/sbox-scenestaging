using Sandbox;
using Sandbox.Diagnostics;
using System.Collections.Generic;

public abstract class ColliderBaseComponent : BaseComponent, BaseComponent.ExecuteInEditor
{
	List<PhysicsShape> shapes = new();
	protected PhysicsBody ownBody;
	protected PhysicsGroup group;

	[Property] public Surface Surface { get; set; }

	bool _isTrigger;
	[Property] public bool IsTrigger
	{
		get => _isTrigger;
		set
		{
			_isTrigger = value;

			foreach ( var shape in shapes )
			{
				shape.IsTrigger = _isTrigger;
			}
		}
	}

	public override void OnEnabled()
	{
		Assert.IsNull( ownBody );
		Assert.AreEqual( 0, shapes.Count );
		Assert.NotNull( Scene );

		PhysicsBody physicsBody = null;

		// is there a physics body?
		var body = GameObject.GetComponentInParent<PhysicsComponent>( true, true );
		if ( body is not null )
		{
			physicsBody = body.GetBody();

			//
			if ( physicsBody is null )
			{
				return;
			}
		}
		
		if ( physicsBody is null )
		{
		//	var physGroup = new PhysicsGroup()

			physicsBody = new PhysicsBody( Scene.PhysicsWorld );
			physicsBody.BodyType = PhysicsBodyType.Keyframed;
			physicsBody.GameObject = GameObject;
			physicsBody.Transform = Transform.World.WithScale( 1 );
			physicsBody.UseController = true;
			physicsBody.GravityEnabled = false;
			ownBody = physicsBody;
		}

		shapes.AddRange( CreatePhysicsShapes( physicsBody ) );

		foreach ( var shape in shapes )
		{
			shape.IsTrigger = _isTrigger;
			shape.SurfaceMaterial = Surface?.ResourcePath;
		}

		physicsBody.RebuildMass();
		physicsBody.LinearDamping = 1;
		physicsBody.AngularDamping = 1;
	}

	protected abstract IEnumerable<PhysicsShape> CreatePhysicsShapes( PhysicsBody targetBody );

	public override void OnDisabled()
	{
		foreach ( var shape in shapes )
		{
			shape.Remove();
		}

		shapes.Clear();

		ownBody?.Remove();
		ownBody = null;

		group?.Remove();
		group = null;
	}

	protected override void OnPostPhysics()
	{
		if ( group is not null )
		{
			foreach( var body in group.Bodies )
			{
			//	body?.Move( GameObject.WorldTransform, Time.Delta * 4.0f );
			}

			return;
		}

		ownBody?.Move( Transform.World, Time.Delta * 4.0f );
	}

	public void OnPhysicsChanged()
	{
		OnDisabled();
		OnEnabled();
	}
}
