namespace Sandbox;

public abstract class ParticleController : BaseComponent, BaseComponent.ExecuteInEditor
{
	ParticleEffect target;

	public override void OnEnabled()
	{
		target = GetComponentInParent<ParticleEffect>( true, true );
		if ( target is not null )
		{
			target.OnPreStep += OnBeforeStep;
			target.OnStep += OnParticleStep;
			target.OnPostStep += OnAfterStep;
		}
		else
		{
			Log.Warning( $"No particle effect found for {this}" );
		}
	}

	public override void OnDisabled()
	{
		if ( target is not null )
		{
			target.OnPreStep -= OnBeforeStep;
			target.OnStep -= OnParticleStep;
			target.OnPostStep -= OnAfterStep;
		}

		target = null;
	}

	/// <summary>
	/// Called before the particle step
	/// </summary>
	protected virtual void OnBeforeStep( float delta )
	{

	}

	/// <summary>
	/// Called after the particle step
	/// </summary>
	protected virtual void OnAfterStep( float delta )
	{

	}

	/// <summary>
	/// Called for each particle during the particle step. This is super threaded
	/// so you better watch out.
	/// </summary>
	protected abstract void OnParticleStep( Particle particle, float delta );
}
