namespace Sandbox;

//[Expose]
[Title( "Particle Model Renderer" )]
[Category( "Particles" )]
[Icon( "favorite" )]
public sealed class ParticleModelRenderer : ParticleController, Component.ExecuteInEditor
{
	[Property] public List<Model> Models { get; set; } = new List<Model> { Model.Cube };

	[Property] public Material MaterialOverride { get; set; }
	[Property] public ParticleFloat Scale { get; set; } = 1;
	[Property] public bool Shadows { get; set; } = true;

	protected override void OnParticleCreated( Particle p )
	{
		p.AddListener( new ParticleModel( this ), this );
	}
}


class ParticleModel : Particle.BaseListener
{
	public ParticleModelRenderer Renderer;

	SceneObject so;

	public ParticleModel( ParticleModelRenderer renderer )
	{
		Renderer = renderer;
	}

	public override void OnEnabled( Particle p )
	{
		so = new SceneObject( Renderer.Scene.SceneWorld, Random.Shared.FromList( Renderer.Models ) ?? Model.Error );
	}

	public override void OnDisabled( Particle p )
	{
		if ( so is null ) return;

		so.Delete();
	}

	public override void OnUpdate( Particle p, float dt )
	{
		if ( so is null ) return;

		so.Transform = new Transform( p.Position, p.Angles, p.Size * Renderer.Scale.Evaluate( p, 2356 ) );
		so.SetMaterialOverride( Renderer.MaterialOverride );
		so.ColorTint = p.Color.WithAlphaMultiplied( p.Alpha );
		so.Flags.CastShadows = Renderer.Shadows;
	}
}
