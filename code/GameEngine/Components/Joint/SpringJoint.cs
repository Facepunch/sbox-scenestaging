using Sandbox;
using Sandbox.Physics;

[Title( "Spring Joint" )]
[Category( "Physics" )]
[Icon( "waves", "red", "white" )]
public sealed class SpringJoint : Joint
{
	/// <summary>
	/// The stiffness of the spring
	/// </summary>
	[Property]
	public float Frequency
	{
		get => springJoint.IsValid() ? springJoint.SpringLinear.Frequency : default;
		set
		{
			if ( springJoint.IsValid() )
			{
				var springLinear = springJoint.SpringLinear;
				springLinear.Frequency = value;
				springJoint.SpringLinear = springLinear;
			}
		}
	}

	/// <summary>
	/// The damping ratio of the spring, usually between 0 and 1
	/// </summary>
	[Property]
	public float Damping
	{
		get => springJoint.IsValid() ? springJoint.SpringLinear.Damping : default;
		set
		{
			if ( springJoint.IsValid() )
			{
				var springLinear = springJoint.SpringLinear;
				springLinear.Damping = value;
				springJoint.SpringLinear = springLinear;
			}
		}
	}

	/// <summary>
	/// Maximum length it should be allowed to go
	/// </summary>
	[Property]
	public float MaxLength
	{
		get => springJoint.IsValid() ? springJoint.MaxLength : default;
		set
		{
			if ( springJoint.IsValid() )
			{
				springJoint.MaxLength = value;
			}
		}
	}

	/// <summary>
	/// Minimum length it should be allowed to go
	/// </summary>
	[Property]
	public float MinLength
	{
		get => springJoint.IsValid() ? springJoint.MinLength : default;
		set
		{
			if ( springJoint.IsValid() )
			{
				springJoint.MinLength = value;
			}
		}
	}

	private Sandbox.Physics.SpringJoint springJoint;

	protected override PhysicsJoint CreateJoint( PhysicsBody body1, PhysicsBody body2 )
	{
		springJoint = PhysicsJoint.CreateSpring( body1, body2, 0, 0 );
		return springJoint;
	}
}
