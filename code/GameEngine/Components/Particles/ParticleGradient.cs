using System.Text.Json.Serialization;

namespace Sandbox;

public struct ParticleGradient
{
	public ParticleGradient()
	{
	}

	public ValueType Type { readonly get; set; } = ValueType.Constant;
	public EvaluationType Evaluation { readonly get; set; } = EvaluationType.Particle;

	public Gradient GradientA { readonly get; set; } = Color.White;
	public Gradient GradientB { readonly get; set; } = Color.White;
	public Color ConstantA { readonly get; set; } = Color.White;
	public Color ConstantB { readonly get; set; } = Color.White;

	[JsonIgnore]
	public Color ConstantValue
	{
		readonly get => ConstantA;
		set => ConstantA = value;
	}


	public static implicit operator ParticleGradient( Color color )
	{
		return new ParticleGradient { Type = ValueType.Constant, ConstantValue = color };
	}

	public enum ValueType
	{
		Constant,
		Range,
		Gradient
	}

	public enum EvaluationType
	{
		Life,
		Frame,
		Particle
	}

	public readonly Color Evaluate( in float delta, in float randomFixed )
	{
		var d = Evaluation switch
		{
			EvaluationType.Life => delta,
			EvaluationType.Frame => Random.Shared.Float( 0, 1 ),
			EvaluationType.Particle => randomFixed,
			_ => delta,
		};

		switch ( Type )
		{
			case ValueType.Constant:
				{
					return ConstantValue;
				}

			case ValueType.Range:
				{
					return Color.Lerp( ConstantA, ConstantB, d );
				}

			case ValueType.Gradient:
				{
					return GradientA.Evaluate( d );
				}
		}

		return ConstantValue;
	}
}
