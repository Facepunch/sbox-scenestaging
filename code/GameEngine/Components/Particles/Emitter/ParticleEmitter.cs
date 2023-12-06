namespace Sandbox;

public abstract class ParticleEmitter : BaseComponent, BaseComponent.ExecuteInEditor
{
	[Property, Group( "Emitter" )] public bool Loop { get; set; } = true;
	[Property, Group( "Emitter" )] public float Duration { get; set; } = 10.0f;
	[Property, Group( "Emitter" )] public float Delay { get; set; } = 0.0f;
	[Property, Range( 0, 1000 ), Group( "Emitter" )] public float Burst { get; set; } = 100.0f;
	[Property, Range( 0, 1000 ), Group( "Emitter" )] public float Rate { get; set; } = 1.0f;

	public float time;
	float emitted;
	float burst;

	ParticleEffect target;

	protected override void OnEnabled()
	{
		ResetEmitter();

		target = Components.GetInAncestorsOrSelf<ParticleEffect>();
		if ( target is not null )
		{
			target.OnPreStep += OnParticleStep;
		}
		else
		{
			Log.Warning( $"No particle effect found for {this}" );
		}
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();

		if ( target is not null )
		{
			target.OnPreStep -= OnParticleStep;
		}

		target = null;
	}

	public void ResetEmitter()
	{
		emitted = 0;
		time = 0;
		burst = 0;
	}

	void OnParticleStep( float delta )
	{
		if ( target is null ) return;
		if ( !target.Active ) return;

		time += delta;

		float runTime = time - Delay;

		// not started yet
		if ( runTime  < 0 )
		{
			return;
		}	

		bool finished = time > (Duration + Delay);

		if ( finished )
		{
			if ( !Loop )
			{
				if ( Scene.IsEditor )
				{
					// TODO - if this is selected
					ResetEmitter();
				}

				return;
			}

			ResetEmitter();
			return;
		}

		while ( burst < Burst && !target.IsFull )
		{
			burst++;
			Emit( target );
		}

		burst = Burst;

		float targetEmission = Rate * runTime;
		while ( !target.IsFull && emitted < targetEmission )
		{
			emitted++;
			Emit( target );
		}
	}

	public abstract bool Emit( ParticleEffect target );
}
