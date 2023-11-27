namespace Sandbox;

[Title( "Sphere Emitter" )]
[Category( "Particles" )]
[Icon( "radio_button_unchecked" )]
public sealed class ParticleSphereEmitter : ParticleEmitter
{
	[Property, Range( 0, 100 )] public float Radius { get; set; } = 20.0f;
	[Property, Range( -1000, 1000 )] public float Velocity { get; set; } = 100.0f;
	[Property] public bool OnEdge { get; set; } = false;


	public override void DrawGizmos()
	{
		if ( !Gizmo.IsSelected )
			return;

		Gizmo.Draw.Color = Color.White.WithAlpha( 0.1f );
		Gizmo.Draw.LineSphere( 0, Radius );

		// TODO - Sphere Gizmo

	}

	public override bool Emit( ParticleEffect target )
	{
		var random = Vector3.Random;
		var offset = random;
		var radius = Radius * Transform.Scale;
		var pos = Transform.Position;

		if ( OnEdge )
		{
			pos += random.Normal * radius;
		}
		else
		{
			pos += random * radius;
		}

		var p = target.Emit( pos );

		if ( Velocity != 0.0f )
		{
			p.Velocity += offset * Velocity;
		}

		return true;
	}
}
