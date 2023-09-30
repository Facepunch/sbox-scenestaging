using Sandbox;
using Sandbox.Diagnostics;
using System.Linq;

[Title( "Rigid Body" )]
[Category( "Physics" )]
[Icon( "panorama_fish_eye", "red", "white" )]
public class PhysicsComponent : GameObjectComponent
{
	public interface IBodyModifier
	{
		void ModifyBody( PhysicsBody body );
	}

	[Property] public bool Static { get; set; } = false;
	[Property] public bool Gravity { get; set; } = true;

	PhysicsBody _body;

	internal PhysicsBody GetBody()
	{
		OnEnableStateChanged();
		return _body;
	}

	public Vector3 Velocity
	{
		set => _body.Velocity = value;
	}

	public override void OnEnabled()
	{
		Assert.True( _body == null );

		if ( Scene.Active.PhysicsWorld  is null )
		{
			Log.Warning( "Tried to create physics object but no physics world" );
			return;
		}	

		_body = new PhysicsBody( Scene.Active.PhysicsWorld );
		
		//_body.Mass = 1;
		_body.UseController = false;
		_body.BodyType = PhysicsBodyType.Dynamic;
		_body.GameObject = GameObject;
		_body.GravityEnabled = true;
		_body.Sleeping = false;
	//	_body.Velocity = Vector3.Up * 0.01f;
		_body.Transform = GameObject.WorldTransform;

		//foreach( var c in GameObject.Components.Concat( GameObject.Children.SelectMany( x => x.Components ) ).OfType<PhysicsComponent.IBodyModifier>() )
		//{
		//	c.ModifyBody( _body ); 
		//}
	}

	public override void OnDisabled()
	{
		_body.Remove();
		_body = null;
	}

	protected override void OnPostPhysics()
	{
		if ( _body is null ) return;

		var bt = _body.Transform;

		var wt = GameObject.WorldTransform;
		GameObject.WorldTransform = bt.WithScale( wt.Scale );
	}

}
