namespace Sandbox;

[Title( "Cone Emitter" )]
[Category( "Particles" )]
[Icon( "change_history" )]
public sealed class ParticleConeEmitter : ParticleEmitter
{
	[Property, Group( "Placement" )]
	public bool OnEdge { get; set; } = false;
	[Property, Group( "Placement" )]
	public bool InVolume { get; set; } = false;

	[Property, Range( 0, 45 ), Group( "Cone" ), Title( "Angle" )]
	public float ConeAngle { get; set; } = 30.0f;
	[Property, Group( "Cone" ), Title( "Start" )]
	public float ConeNear { get; set; } = 1.0f;
	[Property, Group( "Cone" ), Title( "End" )]
	public float ConeFar { get; set; } = 50.0f;


	protected override void DrawGizmos()
	{
		if ( !Gizmo.IsSelected )
			return;

		Gizmo.Draw.Color = Color.White.WithAlpha( 0.3f );
		Gizmo.Draw.LineCircle( Vector3.Forward * (ConeFar - ConeNear), ConeFar * MathF.Tan( ConeAngle.DegreeToRadian() ) );
		Gizmo.Draw.LineCircle( Vector3.Forward * 0, ConeNear * MathF.Tan( ConeAngle.DegreeToRadian() ) );

	}

	public override bool Emit( ParticleEffect target )
	{
		var len = Random.Shared.Float( ConeNear, ConeFar );
		var radius = len * MathF.Tan( ConeAngle.DegreeToRadian() );

		if ( !OnEdge )
		{
			radius = Random.Shared.Float( 0, radius );
		}

		var pos = Vector3.Forward * 0.1f + Vector3.Forward * (len - ConeNear);

		var angle = Random.Shared.Float( 0, MathF.PI * 2.0f );
		pos += Vector3.Left * MathF.Sin( angle ) * radius;
		pos += Vector3.Up * MathF.Cos( angle ) * radius;

		var emitPos = pos;

		if ( !InVolume && !OnEdge )
		{
			emitPos = 0;
		}

		var p = target.Emit( Transform.World.PointToWorld( emitPos ) );

		p.Velocity = Transform.World.NormalToWorld( pos.Normal ) * p.Velocity.Length;





		return true;
	}
}
