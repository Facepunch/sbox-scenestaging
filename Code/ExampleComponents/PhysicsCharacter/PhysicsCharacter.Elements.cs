public sealed partial class PhysicsCharacter : Component
{
	/// <summary>
	/// Make sure the body and our components are created
	/// </summary>
	void EnsureComponentsCreated()
	{
		Body.CollisionEventsEnabled = true;
		Body.CollisionUpdateEventsEnabled = true;

		BodyCollider = Body.GameObject.GetOrAddComponent<CapsuleCollider>();
		FeetCollider = Body.GameObject.GetOrAddComponent<BoxCollider>();

		Body.Flags = Body.Flags.WithFlag( ComponentFlags.Hidden, !_showRigidBodyComponent );
		BodyCollider.Flags = BodyCollider.Flags.WithFlag( ComponentFlags.Hidden, !_showColliderComponent );
		FeetCollider.Flags = FeetCollider.Flags.WithFlag( ComponentFlags.Hidden, !_showColliderComponent );
	}

	/// <summary>
	/// Update the body dimensions, and change the physical properties based on the current state
	/// </summary>
	void UpdateBody()
	{
		var feetHeight = StepHeight;
		var radius = (BodyRadius * MathF.Sqrt( 2 )) / 2;

		BodyCollider.Radius = radius;
		BodyCollider.Start = Vector3.Up * (BodyHeight - BodyCollider.Radius);
		BodyCollider.End = Vector3.Up * (BodyCollider.Radius + feetHeight - BodyCollider.Radius * 0.20f);
		BodyCollider.Friction = 0.0f;
		BodyCollider.Enabled = true;

		FeetCollider.Scale = new Vector3( BodyRadius, BodyRadius, feetHeight );
		FeetCollider.Center = new Vector3( 0, 0, feetHeight * 0.5f );
		FeetCollider.Friction = IsOnGround ? 10f : 0;
		FeetCollider.Enabled = true;

		var locking = Body.Locking;
		locking.Pitch = true;
		locking.Yaw = true;
		locking.Roll = true;
		Body.Locking = locking;

		Body.MassOverride = BodyMass;

		// Move the center of mass to the 
		Body.OverrideMassCenter = true;

		float massCenter = IsOnGround ? WishVelocity.Length.Clamp( 0, StepHeight ) : BodyHeight * 0.5f;
		Body.MassCenterOverride = Body.MassCenterOverride.LerpTo( new Vector3( 0, 0, massCenter ), Time.Delta * 10 );

		Mode?.UpdateRigidBody( Body );
	}
}
