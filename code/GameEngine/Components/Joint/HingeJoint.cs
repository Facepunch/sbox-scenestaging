using Sandbox;
using Sandbox.Physics;

[Title( "Hinge Joint" )]
[Category( "Physics" )]
[Icon( "door_front", "red", "white" )]
public sealed class HingeJoint : Joint
{
	[Property] public Vector3 Center { get; set; }
	[Property] public Vector3 Axis { get; set; } = Vector3.Forward;

	protected override PhysicsJoint CreateJoint( PhysicsBody body1, PhysicsBody body2 )
	{
		return PhysicsJoint.CreateHinge( body1, body2, body1.Transform.PointToWorld( Center ), Axis );
	}

	public override void DrawGizmos()
	{
		base.DrawGizmos();

		Gizmo.Draw.LineThickness = 1;
		Gizmo.Draw.Color = Gizmo.Colors.Green.WithAlpha( Gizmo.IsSelected ? 1.0f : 0.2f );
		Gizmo.Draw.LineBBox( new BBox( Center - 5, Center + 5 ) );
	}
}
