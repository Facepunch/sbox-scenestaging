using Sandbox;

public sealed class DeleteAfterTime : Component
{
	private RealTimeSince _timeSinceSpawn;

	[Property] public float Lifetime { get; set; } = 2f;

	protected override void OnEnabled()
	{
		base.OnEnabled();

		_timeSinceSpawn = 0f;
	}

	protected override void OnUpdate()
	{
		if ( _timeSinceSpawn > Lifetime )
			GameObject.Destroy();
	}
}
