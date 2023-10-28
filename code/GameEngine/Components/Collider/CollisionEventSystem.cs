
using Sandbox;
using System;
using System.Collections.Generic;
using static BaseComponent;

/// <summary>
/// Used to abstract the listening of collision events
/// </summary>
class CollisionEventSystem : IDisposable
{
	private PhysicsBody body;
	
	internal HashSet<Collider> Touching;

	public CollisionEventSystem( PhysicsBody body )
	{
		this.body = body;
		this.body.OnIntersectionStart += OnPhysicsTouchStart;
		this.body.OnIntersectionUpdate += OnPhysicsTouchStay;
		this.body.OnIntersectionEnd += OnPhysicsTouchStop;
	}

	private void OnPhysicsTouchStart( PhysicsIntersection c )
	{
		var o = new Collision( new CollisionSource( c.Self ), new CollisionSource( c.Other ), c.Contact );

		if ( o.Self.Collider == null ) return;
		if ( o.Other.Collider == null ) return;

		if ( o.Self.Collider.IsTrigger || o.Other.Collider.IsTrigger )
		{
			if ( c.Other.Shape.Collider is not Collider bc )
				return;

			Touching ??= new();

			// already added if false
			if ( !Touching.Add( bc ) )
				return;

			bc.OnComponentDeactivated += RemoveDeactivated;

			o.Self.GameObject.ForEachComponent<ITriggerListener>( "OnTriggerEnter", true, ( c ) => c.OnTriggerEnter( bc ) );
		}
		else
		{
			o.Self.GameObject.ForEachComponent<ICollisionListener>( "OnCollisionStart", true, ( x ) => x.OnCollisionStart( o ) );
		}
	}

	private void RemoveDeactivated()
	{
		if ( Touching is null )
			return;

		Action actions = default;

		foreach ( var e in Touching )
		{
			if ( e.Active ) continue;

			actions += () => Touching.Remove( e );
		}

		actions?.Invoke();
	}

	private void OnPhysicsTouchStop( PhysicsIntersectionEnd c )
	{
		var o = new CollisionStop( new CollisionSource( c.Self ), new CollisionSource( c.Other ) );

		if ( o.Self.Collider == null ) return;
		if ( o.Other.Collider == null ) return;

		if ( o.Self.Collider.IsTrigger || o.Other.Collider.IsTrigger )
		{
			if ( c.Other.Shape.Collider is not Collider bc )
				return;

			if ( Touching is null )
				return;

			if ( !Touching.Remove( bc ) )
				return;

			bc.OnComponentDeactivated -= RemoveDeactivated;

			o.Self.GameObject.ForEachComponent<ITriggerListener>( "OnTriggerExit", true, ( c ) => c.OnTriggerExit( bc ) );
		}
		else
		{

			o.Self.GameObject.ForEachComponent<ICollisionListener>( "OnCollisionStop", true, ( x ) => x.OnCollisionStop( o ) );
		}
	}

	private void OnPhysicsTouchStay( PhysicsIntersection c )
	{
		var o = new Collision( new CollisionSource( c.Self ), new CollisionSource( c.Other ), c.Contact );

		o.Self.GameObject.ForEachComponent<ICollisionListener>( "OnCollisionUpdate", true, ( x ) => x.OnCollisionUpdate( o ) );
	}

	public void Dispose()
	{
		if ( !body.IsValid() )
			return;

		body.OnIntersectionStart -= OnPhysicsTouchStart;
		body.OnIntersectionUpdate -= OnPhysicsTouchStay;
		body.OnIntersectionEnd -= OnPhysicsTouchStop;
	}
}
