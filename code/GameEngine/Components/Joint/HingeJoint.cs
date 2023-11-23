using Sandbox;
using Sandbox.Physics;

[Title( "Hinge Joint" )]
[Category( "Physics" )]
[Icon( "door_front", "red", "white" )]
public sealed class HingeJoint : Joint
{
	/// <summary>
	/// Relative position of the hinge
	/// </summary>
	[Property] public Vector3 Center { get; set; }

	/// <summary>
	/// Axis the hinge rotates around
	/// </summary>
	[Property] public Vector3 Axis { get; set; } = Vector3.Forward;

	private float maxAngle;
	private float minAngle;
	private float friction;

	/// <summary>
	/// Maximum angle it should be allowed to go
	/// </summary>
	[Property] 
	public float MaxAngle
	{
		get => maxAngle;
		set
		{
			maxAngle = value;

			if ( hingeJoint.IsValid() )
			{
				hingeJoint.MaxAngle = value;
			}
		}
	}

	/// <summary>
	/// Minimum angle it should be allowed to go
	/// </summary>
	[Property]
	public float MinAngle
	{
		get => minAngle;
		set
		{
			minAngle = value;

			if ( hingeJoint.IsValid() )
			{
				hingeJoint.MinAngle = value;
			}
		}
	}

	/// <summary>
	/// Hinge friction
	/// </summary>
	[Property]
	public float Friction
	{
		get => friction;
		set
		{
			friction = value;

			if ( hingeJoint.IsValid() )
			{
				hingeJoint.Friction = value;
			}
		}
	}

	private Sandbox.Physics.HingeJoint hingeJoint;

	protected override PhysicsJoint CreateJoint( PhysicsBody body1, PhysicsBody body2 )
	{
		hingeJoint = PhysicsJoint.CreateHinge( body1, body2, body1.Transform.PointToWorld( Center ), Axis );
		hingeJoint.MinAngle = MinAngle;
		hingeJoint.MaxAngle = MaxAngle;
		hingeJoint.Friction = Friction;
		return hingeJoint;
	}

	public override void DrawGizmos()
	{
		base.DrawGizmos();

		Gizmo.Draw.LineThickness = 1;
		Gizmo.Draw.Color = Gizmo.Colors.Green.WithAlpha( Gizmo.IsSelected ? 1.0f : 0.2f );
		Gizmo.Draw.LineBBox( new BBox( Center - 5, Center + 5 ) );
	}
}
