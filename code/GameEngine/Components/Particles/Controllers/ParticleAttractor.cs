namespace Sandbox;

[Title( "Particle Attractor" )]
[Category( "Particles" )]
[Icon( "attractions" )]
public class ParticleAttractor : ParticleController
{
	[Property]
	public GameObject Target { get; set; }

	[Property]
	public ParticleFloat Force { get; set; } = 2.0f;

	[Property]
	public ParticleFloat MaxForce { get; set; } = 10.0f;

	[Property]
	public ParticleFloat Randomness { get; set; } = 0.0f;

	[Property]
	public float Radius { get; set; } = 0.0f;


	Vector3? targetPosition;

	public override void DrawGizmos()
	{
		base.DrawGizmos();

		if ( !Target.IsValid() )
			return;

		if ( Gizmo.IsSelected )
		{
			Gizmo.Transform = global::Transform.Zero;
			Gizmo.Draw.Color = Color.White;
			Gizmo.Draw.LineSphere( Target.Transform.Position, Radius );
		}

	}

	protected override void OnBeforeStep( float delta )
	{
		targetPosition = Target?.Transform.Position;
	}

	protected override void OnParticleStep( Particle particle, float delta )
	{
		if ( !targetPosition.HasValue ) return;

		Vector3 target = targetPosition.Value;
		var force = Force.Evaluate( delta, particle.Random04 );
		var maxforce = MaxForce.Evaluate( delta, particle.Random05 );
		var randomNess = Randomness.Evaluate( delta, particle.Random06 );

		if ( Radius > 0 )
		{
			target += new Vector3( particle.Random03 - 0.5f, particle.Random05 - 0.5f, particle.Random04 - 0.5f ).Normal * Radius * particle.Random01 * 2.0f;
		}

		if ( randomNess > 0 )
		{
			target += Vector3.Random * randomNess;
		}

		var dir = (target - particle.Position);
		var distance = dir.Length;
		dir = dir.Normal;

		if ( distance > maxforce ) distance = maxforce;

		particle.Velocity += dir.Normal * delta * force * distance;
	}
}
