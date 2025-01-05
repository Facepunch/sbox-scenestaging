using Sandbox;

public sealed class NoClip : Component
{
	[Property] public float MoveSpeed { get; set; } = 600.0f;

	Angles eyeAngles;

	protected override void OnStart()
	{
		eyeAngles = WorldRotation;
	}

	protected override void OnUpdate()
	{
		eyeAngles += Input.AnalogLook;

		Vector3 movement = Input.AnalogMove;

		WorldRotation = eyeAngles;

		if ( !movement.IsNearlyZero() )
		{
			WorldPosition += WorldRotation * movement.Normal * Time.Delta * MoveSpeed;
		}
	}
}
