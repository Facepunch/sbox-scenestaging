using Sandbox;
using Sandbox.Diagnostics;
using System.Linq;
using System.Text.Json.Serialization;

[Title( "Rigid Body" )]
[Category( "Physics" )]
[Icon( "panorama_fish_eye", "red", "white" )]
public class PhysicsComponent : BaseComponent
{
	[Property] public bool Static { get; set; } = false;
	[Property] public bool Gravity { get; set; } = true;

	PhysicsBody _body;

	internal PhysicsBody GetBody()
	{
		return _body;
	}

	public Vector3 Velocity
	{
		get => _body.Velocity;
		set => _body.Velocity = value;
	}

	public override void OnEnabled()
	{
		Assert.True( _body == null );
		Assert.NotNull( Scene, "Tried to create physics object but no scene" );
		Assert.NotNull( Scene.PhysicsWorld, "Tried to create physics object but no physics world" );

		_body = new PhysicsBody( Scene.PhysicsWorld );
		
		_body.UseController = false;
		_body.BodyType = PhysicsBodyType.Dynamic;
		_body.GameObject = GameObject;
		_body.GravityEnabled = Gravity;
		_body.Sleeping = false;
		_body.Transform = GameObject.WorldTransform;

		GameObject.OnLocalTransformChanged += OnLocalTransformChanged;

		UpdateColliders();
	}

	public override void OnDisabled()
	{
		GameObject.OnLocalTransformChanged -= OnLocalTransformChanged;

		_body.Remove();
		_body = null;

		UpdateColliders();
	}

	bool isUpdatingPositionFromPhysics;

	protected override void OnPostPhysics()
	{
		if ( _body is null ) return;

		_body.GravityEnabled = Gravity;

		var bt = _body.Transform;

		isUpdatingPositionFromPhysics = true;
		var wt = GameObject.WorldTransform;
		GameObject.WorldTransform = bt.WithScale( wt.Scale );
		isUpdatingPositionFromPhysics = false;
	}

	void OnLocalTransformChanged( Transform newTransform )
	{
		if ( isUpdatingPositionFromPhysics ) return;

		if ( _body is not null )
		{
			_body.Transform = GameObject.WorldTransform;
		}
	}

	/// <summary>
	/// Tell child colliders that our physics have changed
	/// </summary>
	void UpdateColliders()
	{
		foreach( var c in GameObject.GetComponents<ColliderBaseComponent>( true, true ) )
		{
			c.OnPhysicsChanged();
		}
	}

}
