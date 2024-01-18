namespace Sandbox;

//[Expose]
[Title( "Particle Model Renderer" )]
[Category( "Particles" )]
[Icon( "favorite" )]
public sealed class ParticleModelRenderer : Component, Component.ExecuteInEditor
{
	[Property] public List<Model> Models { get; set; } = new List<Model> { Model.Cube };

	[Property] public Material MaterialOverride { get; set; }


	ParticleEffect effect;

	protected override void OnEnabled()
	{
		effect = Components.Get<ParticleEffect>( true );
		if ( effect.IsValid() )
		{
			effect.OnParticleCreated += OnParticleCreated;
			effect.OnParticleDestroyed += OnParticleDestroyed;
		}
	}

	protected override void OnDisabled()
	{
		if ( effect.IsValid() )
		{
			foreach ( var p in effect.Particles )
			{
				OnParticleDestroyed( p );
			}

			effect.OnParticleCreated -= OnParticleCreated;
			effect.OnParticleDestroyed -= OnParticleDestroyed;
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
		if ( effect is null || effect.Particles.Count == 0 )
			return;

		if ( Models is null || Models.Count == 0 )
			return;

		Sandbox.Utility.Parallel.ForEach( effect.Particles, p =>
		{
			SceneObject so = p.Get<SceneObject>( "so_model" );
			if ( so is null ) return;

			so.SetMaterialOverride( MaterialOverride );
			so.Transform = new Transform( p.Position, p.Angles, p.Size * 1 );
			so.ColorTint = p.Color.WithAlphaMultiplied( p.Alpha );
		} );

	}
}
