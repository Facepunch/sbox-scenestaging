using Sandbox;

public sealed class SpinComponent : BaseComponent
{
	[Property] public Angles SpinAngles { get; set; }

	public override void Update()
	{
		Transform.Rotation *= (SpinAngles * Time.Delta).ToRotation();
	}
}
