namespace Sandbox;

/// <summary>
/// Rectangle Light, missing features and doesn't play too nice with tiled optimizations yet
/// </summary>
[Title( "Point Light (Staging)" )]
[Icon( "light_mode" )]
[EditorHandle( "materials/gizmo/spotlight.png" )]
public class PointLight2 : PointLight
{
	[Property]
	public float Size
	{
		get;
		set
		{
			if ( field == value ) return;
			field = value;
			UpdateShape();
		}
	} = 100;

	[Property]
	public Texture Cookie
	{
		get;
		set
		{
			if ( field == value ) return;
			field = value;

			if ( _so.IsValid() )
				_so.LightCookie = value;
		}
	}

	ScenePointLight _so;

	protected override ScenePointLight CreateSceneObject()
	{
		_so = base.CreateSceneObject();

		if ( _so.IsValid() )
		{
			_so.LightCookie = Cookie;
			_so.Shape = SceneLight.LightShape.Sphere;
			_so.ShapeSize = Size;
		}

		return _so;
	}

	void UpdateShape()
	{
		if ( !_so.IsValid() )
			return;

		_so.Shape = SceneLight.LightShape.Sphere;
		_so.ShapeSize = Size;
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
