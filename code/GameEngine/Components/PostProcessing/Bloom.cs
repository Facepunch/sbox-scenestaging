using Sandbox;
using System;

// TODO - requires camera component
[Title( "Bloom" )]
[Category( "Post Processing" )]
[Icon( "exposure" )]
public class Bloom : BaseComponent, CameraComponent.ISceneCameraSetup
{
	[Property] public SceneCamera.BloomAccessor.BloomMode Mode { get; set; }

	[Range( 0, 10 )]
	[Property] public float Strength { get; set; } = 1.0f;

	[Range( 0, 2 )]
	[Property] public float Threshold { get; set; } = 0.5f;

	[Range( 0, 5 )]
	[Property] public float ThresholdWidth { get; set; }
	[Property] public Curve BloomCurve { get; set; } = new Curve( new Curve.Frame( 0.0f, 0.5f ), new Curve.Frame( 1.0f, 1.0f ) );
	[Property] public Gradient BloomColor { get; set; } = new Gradient( new Gradient.ColorFrame( 0.0f, Color.White ), new Gradient.ColorFrame( 1.0f, Color.White ) );

	public void SetupCamera( CameraComponent camera, SceneCamera sceneCamera )
	{
		sceneCamera.Bloom.Enabled = true;
		sceneCamera.Bloom.Mode = Mode;
		sceneCamera.Bloom.Strength = Strength;
		sceneCamera.Bloom.Threshold = Threshold;
		sceneCamera.Bloom.ThresholdWidth = ThresholdWidth;

		sceneCamera.Bloom.BlurWeight0 = BloomCurve.EvaluateDelta( 0.00f );
		sceneCamera.Bloom.BlurWeight1 = BloomCurve.EvaluateDelta( 0.25f );
		sceneCamera.Bloom.BlurWeight2 = BloomCurve.EvaluateDelta( 0.50f );
		sceneCamera.Bloom.BlurWeight3 = BloomCurve.EvaluateDelta( 0.75f );
		sceneCamera.Bloom.BlurWeight4 = BloomCurve.EvaluateDelta( 1.00f );

		sceneCamera.Bloom.BlurTint0 = BloomColor.Evaluate( 0.00f );
		sceneCamera.Bloom.BlurTint1 = BloomColor.Evaluate( 0.25f );
		sceneCamera.Bloom.BlurTint2 = BloomColor.Evaluate( 0.50f );
		sceneCamera.Bloom.BlurTint3 = BloomColor.Evaluate( 0.75f );
		sceneCamera.Bloom.BlurTint4 = BloomColor.Evaluate( 1.00f );
	}

}
