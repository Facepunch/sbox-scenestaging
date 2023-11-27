using System.Text.Json.Serialization;

namespace Sandbox;

public struct ParticleFloat
{
	public ValueType Type { readonly get; set; }
	public EvaluationType Evaluation { readonly get; set; }

	public Curve CurveA { readonly get; set; }
	public Curve CurveB { readonly get; set; }

	[JsonInclude]
	public Vector4 Constants;

	[JsonIgnore]
	public float ConstantValue
	{
		readonly get => Constants.x;
		set => Constants.x = value;
	}

	[JsonIgnore]
	public float ConstantA
	{
		readonly get => Constants.x;
		set => Constants.x = value;
	}

	[JsonIgnore]
	public float ConstantB
	{
		readonly get => Constants.y;
		set => Constants.y = value;
	}


	public static implicit operator ParticleFloat( float v )
	{
		return new ParticleFloat { Type = ValueType.Constant, ConstantValue = v };
	}

	public enum ValueType
	{
		Constant,
		Range,
		Curve
	}

	public enum EvaluationType
	{
		Life,
		Frame,
		Particle
	}

	public readonly float Evaluate( in float delta, in float randomFixed )
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
					return MathX.Lerp( ConstantA, ConstantB, d );
				}

			case ValueType.Curve:
				{
					return CurveA.Evaluate( d );
				}
		}

		return ConstantValue;
	}
}
