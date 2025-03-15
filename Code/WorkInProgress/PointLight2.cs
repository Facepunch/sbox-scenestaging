namespace Sandbox;

/// <summary>
/// Rectangle Light, missing features and doesn't play too nice with tiled optimizations yet
/// </summary>
[Title("Point Light (Staging)")]
[Icon("light_mode")]
[EditorHandle( "materials/gizmo/spotlight.png" )]
public class PointLight2 : PointLight
{
	[Property, MakeDirty] public float Size { get; set; } = 100;
    [Property, MakeDirty] public Texture Cookie { get; set; }

    protected override void UpdateSceneObject( SceneLight o )
	{
		base.UpdateSceneObject( o );
        o.LightCookie = Cookie;

		if ( o is SceneLight light )
		{
			light.Shape = SceneLight.LightShape.Sphere;
			light.ShapeSize = Size;
		}
	}

	protected override void DrawGizmos()
	{
		using var scope = Gizmo.Scope( $"light-{GetHashCode()}" );

		Gizmo.Draw.Color = LightColor.WithAlpha( Gizmo.IsSelected ? 0.5f : 0.15f );
		
		Gizmo.Draw.SolidSphere( Vector3.Zero, Size );

		if ( !Gizmo.IsSelected )
			return;

		//Gizmo.Control.BoundingBox( "Size", box, out box );
		//Size = new Vector2( box.Size.z, box.Size.y );

	}
}
