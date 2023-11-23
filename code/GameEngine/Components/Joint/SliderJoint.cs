using Sandbox;
using Sandbox.Physics;

[Title( "Slide Joint" )]
[Category( "Physics" )]
[Icon( "open_in_full", "red", "white" )]
public sealed class SliderJoint : Joint
{
	[Property] public Vector3 Axis { get; set; } = Vector3.Forward;

	[Property]
	public float MaxLength
	{
		get => sliderJoint.IsValid() ? sliderJoint.MaxLength : default;
		set
		{
			if ( sliderJoint.IsValid() )
			{
				sliderJoint.MaxLength = value;
			}
		}
	}

	[Property]
	public float MinLength
	{
		get => sliderJoint.IsValid() ? sliderJoint.MinLength : default;
		set
		{
			if ( sliderJoint.IsValid() )
			{
				sliderJoint.MinLength = value;
			}
		}
	}

	private float friction;

	[Property]
	public float Friction
	{
		get => friction;
		set
		{
			friction = value;

			if ( sliderJoint.IsValid() )
			{
				sliderJoint.Friction = value;
			}
		}
	}

	private Sandbox.Physics.SliderJoint sliderJoint;

	protected override PhysicsJoint CreateJoint( PhysicsBody body1, PhysicsBody body2 )
	{
		sliderJoint = PhysicsJoint.CreateSlider( body1, body2, Axis, MinLength, MaxLength );
		return sliderJoint;
	}
}
