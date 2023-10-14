using Sandbox;
using System;
using static System.Net.Mime.MediaTypeNames;

[Title( "Gradient Fog" )]
[Category( "Rendering" )]
[Icon( "foggy" )]
public class GradientFog : BaseComponent, BaseComponent.ExecuteInEditor
{
	[Property] public float StartDistance { get; set; } = 0.0f;
	[Property] public float EndDistance { get; set; } = 1024.0f;
	[Property] public float FalloffExponent { get; set; } = 1.0f;
	[Property] public Color Color { get; set; } = Color.White;

	[Property] public float Height { get; set; } = 100.0f;
	[Property] public float VerticalFalloffExponent { get; set; } = 1.0f;

	public override void Update()
	{
		var world = Scene?.SceneWorld;
		if ( world is null ) return;

		world.GradientFog.Enabled = true;
		world.GradientFog.StartDistance = StartDistance;
		world.GradientFog.EndDistance = EndDistance;
		world.GradientFog.Color = Color.WithAlpha( 1 );
		world.GradientFog.DistanceFalloffExponent = FalloffExponent;
		world.GradientFog.MaximumOpacity = Color.a;
		world.GradientFog.VerticalFalloffExponent = VerticalFalloffExponent;
		world.GradientFog.StartHeight = Transform.Position.z;
		world.GradientFog.EndHeight = Transform.Position.z + MathF.Max( 1, Height );
	}
}
