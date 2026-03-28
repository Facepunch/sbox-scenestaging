namespace Sandbox;

/// <summary>
/// Rectangle Light, missing features and doesn't play too nice with tiled optimizations yet
/// </summary>
[Title( "Rectangular Light (SceneStaging)" )]
[Icon( "light_mode" )]
[EditorHandle( "materials/gizmo/spotlight.png" )]
public class RectLight : Light
{
	SceneSpotLight _so;

	public new bool Shadows { get; set; } = false; // Override, right now no shadows for area lights

	[Property]
	public float Radius
	{
		get;
		set
		{
			if ( field == value ) return;
			field = value;

			if ( _so.IsValid() )
				_so.Radius = value;
		}
	} = 1000;

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

	[Property]
	public Vector2 Size
	{
		get;
		set
		{
			if ( field == value ) return;
			field = value;

			if ( _so.IsValid() )
				_so.ShapeSize = value / 2;
		}
	} = new( 100, 100 );

	public RectLight()
	{
		LightColor = "#E9FAFF";
	}

	protected override SceneLight CreateSceneObject()
	{
		_so = new SceneSpotLight( Scene.SceneWorld, WorldPosition, LightColor )
		{
			Radius = Radius,
			LightCookie = Cookie,
			FallOff = 1,
			ConeInner = 90,
			ConeOuter = 90,
			Shape = SceneLight.LightShape.Rectangle,
			ShapeSize = Size / 2
		};

		return _so;
	}

	protected override void OnAwake()
	{
		Tags.Add( "light_rect" );

		base.OnAwake();
	}

	protected override void DrawGizmos()
	{
		using var scope = Gizmo.Scope( $"light-{GetHashCode()}" );

		Gizmo.Draw.Color = LightColor.WithAlpha( Gizmo.IsSelected ? 0.5f : 0.05f );

		var size = new Vector3( 0, Size.x, Size.y );
		var box = new BBox( -size / 2, size / 2 );
		Gizmo.Draw.LineBBox( new BBox( -size / 2, size / 2 ) );

		Gizmo.Draw.SolidBox( box );

		if ( !Gizmo.IsSelected )
			return;

		Gizmo.Control.BoundingBox( "Size", box, out box );
		Size = new Vector2( box.Size.y, box.Size.z );
	}
}
