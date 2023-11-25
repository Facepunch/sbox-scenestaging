using Sandbox;

public sealed class SpinComponent : BaseComponent
{
	[Property] public Angles SpinAngles { get; set; }

	public override void Update()
	{
		if ( IsProxy ) return;

		Transform.LocalRotation *= (SpinAngles * Time.Delta).ToRotation();
	}
}
