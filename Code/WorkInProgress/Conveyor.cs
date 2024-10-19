/// <summary>
/// This is all wrong. At this point it might as well be a trigger and go through the physics objects.
/// </summary>
public sealed class Conveyor : Component, IScenePhysicsEvents
{
	[Property]
	public Vector3 Velocity { get; set; }

	Vector3 pos;
	Rigidbody rBody;

	protected override void OnStart()
	{
		base.OnStart();


	}

	void IScenePhysicsEvents.PrePhysicsStep()
	{
		pos = WorldPosition;

		rBody = GetComponent<Rigidbody>();

		if ( rBody.IsValid() )
		{
			rBody.Velocity = WorldRotation * Velocity;
			rBody.PhysicsBody.Sleeping = false;
		}
	}

	void IScenePhysicsEvents.PostPhysicsStep()
	{
		if ( rBody.IsValid() )
		{
			rBody.WorldPosition = pos;
		}
	}
}
