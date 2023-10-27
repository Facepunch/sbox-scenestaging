using Sandbox;
using Sandbox.Diagnostics;

public abstract partial class BaseComponent
{
	public readonly record struct Collision( CollisionSource Self, CollisionSource Other, PhysicsContact Contact );
	public readonly record struct CollisionStop( CollisionSource Self, CollisionSource Other );

	public readonly struct CollisionSource
	{
		internal CollisionSource( PhysicsContact.Target target )
		{
			Body = target.Body;
			Shape = target.Shape;
			Collider = target.Shape.Collider as Collider;
			GameObject = Collider?.GameObject;
		}

		public readonly PhysicsBody Body;
		public readonly PhysicsShape Shape;
		public readonly Collider Collider;
		public readonly GameObject GameObject;
	}

	/// <summary>
	/// A component with this interface can react to collisions
	/// </summary>
	public interface ICollisionListener
	{
		void OnCollisionStart( Collision other );
		void OnCollisionUpdate( Collision other );
		void OnCollisionStop( CollisionStop other );
	}
}
