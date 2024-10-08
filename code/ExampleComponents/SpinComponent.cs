public sealed class SpinComponent : Component
{
	[Property] public Angles SpinAngles { get; set; }

	protected override void OnFixedUpdate()
	{
		if ( IsProxy ) return;

		Transform.LocalRotation *= (SpinAngles * Time.Delta).ToRotation();
	}
}


public sealed class MoveComponent : Component
{
	[Property] public Vector3 Distance { get; set; }
	[Property]
	public float Speed { get; set; } = 10.0f;

	Transform startPos;

	protected override void OnEnabled()
	{
		base.OnEnabled();

		startPos = LocalTransform;
	}

	protected override void OnFixedUpdate()
	{
		if ( IsProxy ) return;

		LocalPosition = startPos.Position + (Distance * (MathF.Sin( Time.Now * Speed ).Remap( -1, 1 )));
	}
}
