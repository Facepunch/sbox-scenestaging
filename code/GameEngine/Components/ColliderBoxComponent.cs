using Sandbox;
using Sandbox.Diagnostics;

[Title( "Collider - Box" )]
[Category( "Physics" )]
[Icon( "check_box_outline_blank", "red", "white" )]
public class ColliderBoxComponent : GameObjectComponent, PhysicsComponent.IBodyModifier
{
	[Property] public Vector3 Scale { get; set; } = 50;
	[Property] public Surface Surface { get; set; }

	PhysicsShape shape;

	public override void DrawGizmos()
	{
		if ( !Gizmo.IsSelected && !Gizmo.IsHovered )
			return;

		Gizmo.Draw.LineThickness = 1;
		Gizmo.Draw.Color = Gizmo.Colors.Green.WithAlpha( Gizmo.IsSelected ? 1.0f : 0.2f );
		Gizmo.Draw.LineBBox( new BBox( Scale * -0.5f, Scale * 0.5f ) );
	}

	public override void OnEnabled()
	{

	}

	public override void OnDisabled()
	{

	}

	public void ModifyBody( PhysicsBody body )
	{
		var tx = body.Transform.ToLocal( GameObject.WorldTransform );

		// todo position relative to body
		shape = body.AddBoxShape( tx.Position, tx.Rotation, Scale * 0.5f );
		if ( shape is null ) return;

		if ( Surface is not null )
		{
			shape.SurfaceMaterial = Surface.ResourceName;
		}
	}

	protected override void OnPreRender()
	{
		if ( shape is null )
			return;

		
	}
}
