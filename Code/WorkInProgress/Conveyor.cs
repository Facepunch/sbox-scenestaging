namespace Sandbox;

/// <summary>
/// Sets velocity on a collider to simulate a conveyor belt
/// </summary>
public sealed class Conveyor : Component
{
	[Property]
	public Vector3 Velocity { get; set; }

	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();

		var collider = GetComponent<Collider>();
		if ( collider.IsValid() )
		{
			collider.SurfaceVelocity = Velocity;
		}
	}
}
