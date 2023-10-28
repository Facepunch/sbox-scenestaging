using Sandbox;

public sealed class ColorOverTime : BaseComponent
{
	[Property] public Gradient Gradient { get; set; } = new Gradient( new Gradient.ColorFrame( 0.0f, Color.Cyan ), new Gradient.ColorFrame( 1.0f, Color.Red ) );
	[Property] public float Speed { get; set; } = 1.0f;

	float delta = 0.0f;

	public override void Update()
	{
		delta += Time.Delta * Speed;

		var color = Gradient.Evaluate( (delta) % 1.0f );

		GameObject.ForEachComponent<ITintable>( "ChangeColor", true, t =>
		{
			t.Color = color;
		} );
	}
}
