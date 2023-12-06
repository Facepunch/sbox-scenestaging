using Sandbox;

public sealed class SelfDestructComponent : BaseComponent
{
	[Property] float Seconds { get; set; }

	TimeUntil timeUntilDie;

	protected override void OnEnabled()
	{
		timeUntilDie = Seconds;
	}

	protected override void OnUpdate()
	{
		if ( GameObject.IsProxy )
			return;

		if ( timeUntilDie <= 0.0f )
		{
			GameObject.Destroy();
		}
	}
}
