/// <summary>
/// This is all wrong. At this point it might as well be a trigger and go through the physics objects.
/// </summary>
public sealed class Conveyor : Component, Component.ICollisionListener
{
	[Property]
	public Vector3 Velocity { get; set; }

	void ICollisionListener.OnCollisionStart( Collision collision ) => ApplyVelocityToCollision( collision );
	void ICollisionListener.OnCollisionUpdate( Collision collision ) => ApplyVelocityToCollision( collision );

	void ApplyVelocityToCollision( Collision collision )
	{
		collision.Other.Shape.EnableTouchPersists = true;

		var point = collision.Contact.Point;
		var vel = WorldTransform.NormalToWorld( Velocity ) * Velocity.Length;

		collision.Other.Body.Velocity = collision.Other.Body.Velocity.AddClamped( vel, vel.Length );
	}
}
