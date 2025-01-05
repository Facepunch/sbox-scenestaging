using Sandbox;

public class DoorComponent : BaseInteractor
{
	[Property] public bool isOpen {get; set; } = false;

	Rotation startRotation;
	Rotation targetRotation;

	protected override void OnStart()
	{
		base.OnStart();

		startRotation = WorldRotation;

		targetRotation = startRotation * Rotation.From( new Angles( 0, 90, 0 ) );
	}

	protected override void OnUpdate()
	{
		if( isOpen )
		{
			WorldRotation = Rotation.Slerp( WorldRotation, targetRotation, Time.Delta * 5.0f );
		}
		else
		{
			WorldRotation = Rotation.Slerp( WorldRotation, startRotation, Time.Delta * 5.0f );
		}
	}
	public override void OnUsed()
	{
		isOpen = !isOpen;
	}
}
