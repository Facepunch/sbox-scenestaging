namespace Sandbox;

//[Expose]
[Title( "Particle Model Renderer" )]
[Category( "Particles" )]
[Icon( "favorite" )]
public sealed class ParticleModelRenderer : ParticleRenderer, Component.ExecuteInEditor
{
	[Property] public List<Model> Models { get; set; } = new List<Model> { Model.Cube };

	[Property] public Material MaterialOverride { get; set; }
	[Property] public ParticleFloat Scale { get; set; } = 1;
	[Property] public bool Shadows { get; set; } = true;

	protected override void OnEnabled()
	{
		if ( ParticleEffect.IsValid() )
		{
			ParticleEffect.OnParticleCreated += OnParticleCreated;
			ParticleEffect.OnParticleDestroyed += OnParticleDestroyed;
		}
	}

	protected override void OnDisabled()
	{
		if ( ParticleEffect.IsValid() )
		{
			foreach ( var p in ParticleEffect.Particles )
			{
				OnParticleDestroyed( p );
			}

			ParticleEffect.OnParticleCreated -= OnParticleCreated;
			ParticleEffect.OnParticleDestroyed -= OnParticleDestroyed;
		}
	}

	private void OnParticleDestroyed( Particle particle )
	{
		SceneObject so = particle.Get<SceneObject>( "so_model" );
		so?.Delete();
	}

	private void OnParticleCreated( Particle particle )
	{
		var so = new SceneObject( Scene.SceneWorld, Random.Shared.FromList( Models ) );
		particle.Set( "so_model", so );
	}

	protected override void OnPreRender()
	{
		if ( ParticleEffect is null || ParticleEffect.Particles.Count == 0 )
			return;

		if ( Models is null || Models.Count == 0 )
			return;

		Sandbox.Utility.Parallel.ForEach( ParticleEffect.Particles, p =>
		{
			SceneObject so = p.Get<SceneObject>( "so_model" );
			if ( so is null ) return;

			so.SetMaterialOverride( MaterialOverride );
			so.Transform = new Transform( p.Position, p.Angles, p.Size * Scale.Evaluate( Time.Delta, p.Random04 ) );
			so.ColorTint = p.Color.WithAlphaMultiplied( p.Alpha );
			so.Flags.CastShadows = Shadows;
		} );

	}
}
