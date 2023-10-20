using Sandbox;
using Sandbox.Diagnostics;
using System.Collections.Generic;

public abstract class ColliderBaseComponent : BaseComponent, BaseComponent.ExecuteInEditor
{
	List<PhysicsShape> shapes = new();
	protected PhysicsBody keyframeBody;

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


	/// <summary>
	/// Overridable in derived component to create shapes
	/// </summary>
	protected abstract IEnumerable<PhysicsShape> CreatePhysicsShapes( PhysicsBody targetBody );

	public override void OnEnabled()
	{
		Assert.IsNull( keyframeBody );
		Assert.AreEqual( 0, shapes.Count );
		Assert.NotNull( Scene );

		UpdatePhysicsBody();
		RebuildImmediately();
	}

	void UpdatePhysicsBody()
	{
		PhysicsBody physicsBody = null;

		// is there a rigid body?
		var body = GameObject.GetComponentInParent<PhysicsComponent>( true, true );
		if ( body is not null )
		{
			physicsBody = body.GetBody();
			if ( physicsBody is null ) return;
		}

		// If not, make us a keyframe body
		if ( physicsBody is null )
		{
			physicsBody = new PhysicsBody( Scene.PhysicsWorld );
			physicsBody.BodyType = PhysicsBodyType.Keyframed;
			physicsBody.GameObject = GameObject;
			physicsBody.Transform = Transform.World.WithScale( 1 );
			physicsBody.UseController = true;
			physicsBody.GravityEnabled = false;
			keyframeBody = physicsBody;

			Transform.OnTransformChanged += UpdateKeyframeTransform;
		}
	}

	protected virtual void RebuildImmediately()
	{
		shapesDirty = false;

		// destroy any old shapes
		foreach ( var shape in shapes )
		{
			shape.Remove();
		}

		shapes.Clear();

		// find our target body
		PhysicsBody physicsBody = keyframeBody;

		// try to get rigidbody
		if ( physicsBody is null )
		{
			var body = GameObject.GetComponentInParent<PhysicsComponent>( true, true );
			if ( body is null ) return;
			physicsBody = body.GetBody();
		}

		// no physics body
		if ( physicsBody is null ) return;

		// create the new shapes
		shapes.AddRange( CreatePhysicsShapes( physicsBody ) );

		// configure shapes
		ConfigureShapes();

		// store the scale in which we were built
		_buildScale = Transform.Scale;
	}

	public override void Update()
	{
		if ( shapesDirty )
		{
			RebuildImmediately();
		}
	}

	/// <summary>
	/// Apply any things that we an apply after they're created
	/// </summary>
	protected void ConfigureShapes()
	{
		foreach ( var shape in shapes )
		{
			shape.IsTrigger = _isTrigger;
			shape.SurfaceMaterial = Surface?.ResourcePath;
		}
	}

	public override void OnDisabled()
	{
		foreach ( var shape in shapes )
		{
			shape.Remove();
		}

		shapes.Clear();

		Transform.OnTransformChanged -= UpdateKeyframeTransform;

		keyframeBody?.Remove();
		keyframeBody = null;
	}

	public void OnPhysicsChanged()
	{
		OnDisabled();
		OnEnabled();
	}

	bool shapesDirty;

	protected void Rebuild()
	{
		shapesDirty = true;
	}

	Vector3 _buildScale;

	void UpdateKeyframeTransform()
	{
		if ( Transform.Scale != _buildScale )
		{
			Rebuild();
		}

		if ( Scene.IsEditor )
		{
			keyframeBody.Transform = Transform.World;
		}
		else
		{
			keyframeBody.Transform = keyframeBody.Transform.WithScale( Transform.World.Scale );

			// if timeToArrive is longer than a physics frame delta, the objects that we push
			// will get pushed smoother, but will clip inside the collider more.
			// if it's shorter, the objects will be punched quicker than the collider is moving
			// so will tend to over-react to being touched.
			float timeToArrive = Scene.FixedDelta;

			keyframeBody.Move( Transform.World, timeToArrive );
		}
	}
}
