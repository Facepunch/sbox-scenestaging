namespace Sandbox;

[Title( "Box Emitter" )]
[Category( "Particles" )]
[Icon( "check_box_outline_blank" )]
public sealed class ParticleBoxEmitter : ParticleEmitter
{
	[Property] public Vector3 Size { get; set; } = 50.0f;
	[Property] public bool OnEdge { get; set; } = false;

	protected override void DrawGizmos()
	{
		if ( !Gizmo.IsSelected )
			return;

		Gizmo.Draw.Color = Color.White.WithAlpha( 0.1f );
		Gizmo.Draw.LineBBox( BBox.FromPositionAndSize( 0, Size ) );

		// TODO - Box Resize Gizmo

	}

	public override bool Emit( ParticleEffect target )
	{
		var bbox = BBox.FromPositionAndSize( Transform.Position, Size );

		var pos = Size * -0.5f;

		var size = Size;

		size.x *= Random.Shared.Float( 0.0f, 1.0f );
		size.y *= Random.Shared.Float( 0.0f, 1.0f );
		size.z *= Random.Shared.Float( 0.0f, 1.0f );

		if ( OnEdge )
		{
			var face = Random.Shared.Int( 0, 5 );
			if ( face == 0 ) size.x = 0;
			else if ( face == 1 ) size.y = 0;
			else if ( face == 2 ) size.z = 0;
			else if ( face == 3 ) size.x = Size.x;
			else if ( face == 4 ) size.y = Size.y;
			else if ( face == 5 ) size.z = Size.z;
		}

		pos += size;

		target.Emit( Transform.World.PointToWorld( pos ) );

		return true;
	}
}
