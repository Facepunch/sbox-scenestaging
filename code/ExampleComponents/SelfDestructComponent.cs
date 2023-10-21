using Sandbox;

public sealed class SelfDestructComponent : BaseComponent
{
	[Property] float Seconds { get; set; }

	TimeUntil timeUntilDie;

	public override void OnEnabled()
	{
		timeUntilDie = Seconds;
	}

	public override void Update()
	{
		if ( timeUntilDie <= 0.0f )
		{
			GameObject.Destroy();
		}
	}
}
