namespace Sandbox;

[Title( "Capsule Light (Staging)" )]
[Icon( "light_mode" )]
[EditorHandle( "materials/gizmo/spotlight.png" )]
public class CapusleLight : PointLight
{
	ScenePointLight _so;

	public new bool Shadows { get; set; } = false; // Override, right now no shadows for area lights

	[Property]
	public float Length
	{
		get;
		set
		{
			var v = MathF.Max( 0.0f, value );
			if ( field == v ) return;
			field = v;
			UpdateShape();
		}
	} = 100.0f;

	[Property]
	public new float Radius
	{
		get;
		set
		{
			var v = MathF.Max( 0.0f, value );
			if ( field == v ) return;
			field = v;
			UpdateShape();
		}
	} = 5.0f;

	protected override ScenePointLight CreateSceneObject()
	{
		_so = base.CreateSceneObject();

		UpdateShape();

		return _so;
	}

	void UpdateShape()
	{
		if ( !_so.IsValid() )
			return;

		_so.Radius = MathF.Max( Length * MathF.PI, 256.0f );
		_so.Shape = SceneLight.LightShape.Capsule;
		_so.ShapeSize = new Vector2( Radius, Length );
	}

	protected override void DrawGizmos()
	{
		using var scope = Gizmo.Scope( $"light-{GetHashCode()}" );

		Gizmo.Draw.Color = LightColor.WithAlpha( Gizmo.IsSelected ? 0.5f : 0.15f );

		var capsule = new Capsule( Vector3.Backward * Length * 0.5f, Vector3.Forward * Length * 0.5f, Radius );
		Gizmo.Draw.LineCapsule( capsule );

		if ( !Gizmo.IsSelected )
			return;

		Gizmo.Draw.Color = Gizmo.Colors.Red.WithAlpha( 0.5f );

		Gizmo.Transform = Transform.World.Add( Vector3.Forward * Length * 0.5f, false );
		if ( Gizmo.Control.Arrow( "length-fwd", Vector3.Forward, out var length, girth: 32.0f, length: 16.0f ) )
			Length += length;

		Gizmo.Transform = Transform.World.Add( Vector3.Backward * Length * 0.5f, false );
		if ( Gizmo.Control.Arrow( "length-back", Vector3.Backward, out var length2, girth: 32.0f, length: 16.0f ) )
			Length += length2;

		Gizmo.Draw.Color = Gizmo.Colors.Green.WithAlpha( 0.5f );
		Gizmo.Transform = Transform.World;
		Gizmo.Draw.LineCircle( Vector3.Zero, Radius + 8.0f );

		Gizmo.Transform = Transform.World.Add( Vector3.Right * (Radius + 8), false );
		if ( Gizmo.Control.Arrow( "radius", Vector3.Right, out var radius, girth: 16.0f, length: 8.0f ) )
			Radius += radius;

		// Sanity check in editor
		Radius = MathF.Max( Radius, 0.0f );
		Length = MathF.Max( Length, 0.0f );
	}
}
