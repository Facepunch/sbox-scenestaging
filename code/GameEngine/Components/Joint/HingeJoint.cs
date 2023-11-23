using Sandbox;
using Sandbox.Physics;

[Title( "Hinge Joint" )]
[Category( "Physics" )]
[Icon( "door_front", "red", "white" )]
public sealed class HingeJoint : Joint
{
	[Property] public Vector3 Center { get; set; }
	[Property] public Vector3 Axis { get; set; } = Vector3.Forward;

	[Property] 
	public float MaxAngle
	{
		get => hingeJoint.IsValid() ? hingeJoint.MaxAngle : default;
		set
		{
			if ( hingeJoint.IsValid() )
			{
				hingeJoint.MaxAngle = value;
			}
		}
	}

	[Property]
	public float MinAngle
	{
		get => hingeJoint.IsValid() ? hingeJoint.MinAngle : default;
		set
		{
			if ( hingeJoint.IsValid() )
			{
				hingeJoint.MinAngle = value;
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
