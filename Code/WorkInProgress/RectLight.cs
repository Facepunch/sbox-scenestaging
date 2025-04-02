namespace Sandbox;

/// <summary>
/// Rectangle Light, missing features and doesn't play too nice with tiled optimizations yet
/// </summary>
[Title("Rectangular Light (SceneStaging)")]
[Icon("light_mode")]
[EditorHandle( "materials/gizmo/spotlight.png" )]
public class RectLight : Light
{
	public new bool Shadows { get; set; } = false; // Override, right now no shadows for area lights
	[Property, MakeDirty] public float Radius { get; set; } = 1000;
	[Property, MakeDirty] public Texture Cookie { get; set; }
	[Property, MakeDirty] public Vector2 Size { get; set; } = new( 100, 100 );

	public RectLight()
	{
		LightColor = "#E9FAFF";
	}

	protected override SceneLight CreateSceneObject()
	{
		var v = new SceneSpotLight( Scene.SceneWorld, WorldPosition, LightColor );
		return v;
	}

	protected override void OnAwake()
	{
		Tags.Add( "light_rect" );

		base.OnAwake();
	}

	protected override void UpdateSceneObject( SceneLight o )
	{
		base.UpdateSceneObject( o );

		o.Radius = Radius;
		o.LightCookie = Cookie;

		if ( o is SceneSpotLight spot )
		{
			spot.FallOff = 1;
			spot.ConeInner = 90;
			spot.ConeOuter = 90;
			spot.Shape = SceneLight.LightShape.Rectangle;
			spot.ShapeSize = Size / 2;
		}
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
