using Sandbox;

public sealed class ColorOverTime : Component
{
	[Property] public Gradient Gradient { get; set; } = new Gradient( new Gradient.ColorFrame( 0.0f, Color.Cyan ), new Gradient.ColorFrame( 1.0f, Color.Red ) );
	[Property] public float Speed { get; set; } = 1.0f;

	float delta = 0.0f;

	protected override void OnUpdate()
	{
		delta += Time.Delta * Speed;

		var color = Gradient.Evaluate( (delta) % 1.0f );

		GameObject.Components.ForEach<ITintable>( "ChangeColor", false, t =>
		{
			t.Color = color;
		} );
	}
}
