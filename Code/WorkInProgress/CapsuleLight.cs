namespace Sandbox;

[Title("Capsule Light (Staging)")]
[Icon("light_mode")]
[EditorHandle( "materials/gizmo/spotlight.png" )]
public class CapusleLight : PointLight2
{
	[Property, MakeDirty] public Vector2 Size { get; set; } = new( 100, 10 );
    
    protected override void UpdateSceneObject( SceneLight o )
	{
		base.UpdateSceneObject( o );
        
		if ( o is SceneLight light )
		{
			light.Shape = SceneLight.LightShape.Capsule;
			light.ShapeSize = Size;
		}
	}

	protected override void DrawGizmos()
	{
		using var scope = Gizmo.Scope( $"light-{GetHashCode()}" );

		Gizmo.Draw.Color = LightColor.WithAlpha( Gizmo.IsSelected ? 0.5f : 0.15f );
		
		//Gizmo.Draw.SolidSphere( Vector3.Zero, Size );

		if ( !Gizmo.IsSelected )
			return;

		//Gizmo.Control.BoundingBox( "Size", box, out box );
		//Size = new Vector2( box.Size.z, box.Size.y );

	}
}
