using Sandbox;
using Sandbox.Diagnostics;
using System.Collections.Generic;

[Title( "Collider - Box" )]
[Category( "Physics" )]
[Icon( "check_box_outline_blank", "red", "white" )]
public class ColliderBoxComponent : Collider
{
	private Vector3 _scale = 50;
	private Vector3 _offset = 0;

	[Property] 
	public Vector3 Scale
	{
		get => _scale;
		set
		{
			if ( _scale == value ) return;

			_scale = value;
			Rebuild();
		}
	}

	[Property]
	public Vector3 Offset
	{
		get => _offset;
		set
		{
			if (_offset == value) return;

			_offset = value;
			Rebuild();
		}
	}

	public override void DrawGizmos()
	{
		if ( !Gizmo.IsSelected && !Gizmo.IsHovered )
			return;

		Gizmo.Draw.LineThickness = 1;
		Gizmo.Draw.Color = Gizmo.Colors.Green.WithAlpha( Gizmo.IsSelected ? 1.0f : 0.2f );
		Gizmo.Draw.LineBBox( new BBox( Scale * -0.5f + Offset, Scale * 0.5f + Offset ) );
	}

	protected override IEnumerable<PhysicsShape> CreatePhysicsShapes( PhysicsBody targetBody )
	{
		var tx = targetBody.Transform.ToLocal( Transform.World );
		var shape = targetBody.AddBoxShape( tx.Position + Offset, tx.Rotation, Scale * 0.5f * tx.Scale );

		if ( Surface is not null )
		{
			shape.SurfaceMaterial = Surface.ResourceName;
		}

		yield return shape;
	}
}
