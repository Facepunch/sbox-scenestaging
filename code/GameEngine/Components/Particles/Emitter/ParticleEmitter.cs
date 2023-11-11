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

	public override void OnEnabled()
	{
		ResetEmitter();
	}

	public void ResetEmitter()
	{
		emitted = 0;
		time = 0;
		burst = 0;
	}

	public override void Update()
	{
		if ( !TryGetComponent( out ParticleEffect effect ) ) 
			return;

		time += Time.Delta;

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

		while ( burst < Burst && !effect.IsFull )
		{
			burst++;
			Emit( effect );
		}

		burst = Burst;

		float targetEmission = Rate * runTime;
		while ( !effect.IsFull && emitted < targetEmission )
		{
			emitted++;
			Emit( effect );
		}
	}

	public abstract bool Emit( ParticleEffect target );
}
