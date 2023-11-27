using Sandbox.Utility;

namespace Sandbox;

public class ParticleNoise : ParticleController
{
	[Property]
	public float WorldScale { get; set; } = 0.4f;

	[Property]
	public float Scale { get; set; } = 1.0f;

	[Property]
	public float Octaves { get; set; } = 2.0f;

	[Property]
	public float Speed { get; set; } = 2.0f;


	float noiseTime;

	protected override void OnBeforeStep( float delta )
	{
		noiseTime += delta * Speed;
	}

	protected override void OnParticleStep( Particle particle, float delta )
	{
		var x = Noise.Fbm( (int)Octaves, particle.Position.y * WorldScale, noiseTime ) - 0.5f;
		var y = Noise.Fbm( (int)Octaves, particle.Position.z * WorldScale, noiseTime ) - 0.5f;
		var z = Noise.Fbm( (int)Octaves, particle.Position.x * WorldScale, noiseTime ) - 0.5f;

		particle.Position = particle.StartPosition + new Vector3( x, y, z ) * Scale;
	}
}
