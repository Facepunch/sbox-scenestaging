using Sandbox;
using Sandbox.Physics;

[Title( "Slider Joint" )]
[Category( "Physics" )]
[Icon( "open_in_full", "red", "white" )]
[EditorHandle( "materials/gizmo/slider.png" )]
public sealed class SliderJoint : Joint
{
	private float maxLength;
	private float minLength;
	private float friction;

	/// <summary>
	/// Maximum length it should be allowed to go
	/// </summary>
	[Property]
	public float MaxLength
	{
		get => maxLength;
		set
		{
			maxLength = value;

			if ( sliderJoint.IsValid() )
			{
				sliderJoint.MaxLength = value;
			}
		}
	}

	/// <summary>
	/// Minimum length it should be allowed to go
	/// </summary>
	[Property]
	public float MinLength
	{
		get => minLength;
		set
		{
			minLength = value;

			if ( sliderJoint.IsValid() )
			{
				sliderJoint.MinLength = value;
			}
		}
	}

	/// <summary>
	/// Slider friction
	/// </summary>
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
		sliderJoint = PhysicsJoint.CreateSlider( body1, body2, Transform.Rotation.Up, MinLength, MaxLength );
		sliderJoint.Friction = friction;
		return sliderJoint;
	}
}
