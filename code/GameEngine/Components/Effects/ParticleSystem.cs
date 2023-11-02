using Sandbox;
using Sandbox.Diagnostics;

[Title( "Particle System" )]
[Category( "Effects" )]
[Icon( "shower" )]
[EditorHandle( "materials/gizmo/particles.png" )]
public class ParticleSystem : BaseComponent, BaseComponent.ExecuteInEditor
{
	Sandbox.ParticleSystem _particles;
	
	[Property] public bool Looped { get; set; } = false;

	[Range( 0, 2.0f )]
	[Property] public float PlaybackSpeed { get; set; } = 1.0f;

	private bool emissionStopped;
	
	/// <summary>
	/// Turn on or off particle emission.
	/// Useful for particles with intermittent or permanent durations.
	/// </summary>
	[Property]
	public bool EmissionStopped
	{
		get => emissionStopped;
		set
		{
			emissionStopped = value;
			
			if (_sceneObject is not null)
				_sceneObject.EmissionStopped = value;
		}
	}

	[Property] public Sandbox.ParticleSystem Particles 
	{
		get => _particles;
		set
		{
			if ( _particles == value ) return;
			_particles = value;

			RecreateSceneObject();
		}
	}

	[Property] public GameObject ControlPoint0 { get; set; }
	[Property] public GameObject ControlPoint1 { get; set; }
	[Property] public GameObject ControlPoint2 { get; set; }

	SceneParticles _sceneObject;
	public SceneParticles SceneObject => _sceneObject;

	public override void DrawGizmos()
	{

	}

	public override void OnEnabled()
	{
		Assert.NotNull( Scene );

		if ( _sceneObject is null )
		{
			RecreateSceneObject();
		}
	}

	void RecreateSceneObject()
	{
		if ( Particles is null )
			return;

		if ( !Enabled )
			return;

		_sceneObject?.Delete();

		_sceneObject = new SceneParticles( Scene.SceneWorld, _particles );
		_sceneObject.Transform = Transform.World;
		_sceneObject.EmissionStopped = emissionStopped;
	}

	public void PlayEffect()
	{
		RecreateSceneObject();
	}

	public void Set( int i, float value )
	{
		_sceneObject?.SetControlPoint( i, value );
	}
	
	public void Set( int i, Vector3 position )
	{
		_sceneObject?.SetControlPoint( i, position );
	}
	
	public void Set( int i, Transform transform )
	{
		_sceneObject?.SetControlPoint( i, transform );
	}
	
	public void Set( int i, Rotation rotation )
	{
		_sceneObject?.SetControlPoint( i, rotation );
	}

	public override void Update()
	{
		if ( !_sceneObject.IsValid() )
		{
			if ( Scene.IsEditor || Looped )
			{
				RecreateSceneObject();
			}

			if ( !_sceneObject.IsValid() )
				return;
		}
		
		_sceneObject.SetControlPoint( 0, ControlPoint0.IsValid() ? ControlPoint0.Transform.World : Transform.World );
		_sceneObject.SetControlPoint( 1, ControlPoint1.IsValid() ? ControlPoint1.Transform.World : Transform.World );
		_sceneObject.SetControlPoint( 2, ControlPoint2.IsValid() ? ControlPoint2.Transform.World : Transform.World );
		
		_sceneObject.Simulate( Time.Delta * PlaybackSpeed );
		
		if ( _sceneObject.Finished )
		{
			_sceneObject?.Delete();
			_sceneObject = null;
		}
	}

	public override void OnDisabled()
	{
		_sceneObject?.Delete();
		_sceneObject = null;
	}

	protected override void OnPreRender()
	{
		if ( !_sceneObject.IsValid() )
			return;

		_sceneObject.Transform = Transform.World;
	}

}
