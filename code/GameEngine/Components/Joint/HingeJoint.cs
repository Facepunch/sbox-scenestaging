using Sandbox;
using Sandbox.Physics;

[Title( "Hinge Joint" )]
[Category( "Physics" )]
[Icon( "door_front", "red", "white" )]
[EditorHandle( "materials/gizmo/hinge.png" )]
public sealed class HingeJoint : Joint
{
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
		hingeJoint = PhysicsJoint.CreateHinge( body1, body2, Transform.Position, Transform.Rotation.Up );
		hingeJoint.MinAngle = MinAngle;
		hingeJoint.MaxAngle = MaxAngle;
		hingeJoint.Friction = Friction;
		return hingeJoint;
	}

	protected override void DrawGizmos()
	{
		base.DrawGizmos();

		Gizmo.Draw.IgnoreDepth = true;
		Gizmo.Draw.LineThickness = 1;
		Gizmo.Draw.Color = Gizmo.Colors.Green.WithAlpha( Gizmo.IsSelected ? 1.0f : 0.2f );
		Gizmo.Draw.LineBBox( new BBox( 0, 10 ) );
		Gizmo.Draw.Color = Gizmo.Colors.Red;
		Gizmo.Draw.Line( Vector3.Up * -5.0f, Vector3.Up * 5.0f );
	}
}
