namespace Sandbox;

public abstract class ParticleEmitter : BaseComponent, BaseComponent.ExecuteInEditor
{
	[Property, Range( 0, 1000 )] public float Initial { get; set; } = 10.0f;
	[Property, Range( 0, 1000 )] public float Rate { get; set; } = 1.0f;

	public float time;

	public override void OnEnabled()
	{
		time = Initial;
	}

	public override void Update()
	{
		if ( !TryGetComponent( out ParticleEffect effect ) ) return;

		time += Time.Delta * Rate;

		while ( !effect.IsFull && time >= 1.0f )
		{
			Emit( effect );
			time -= 1.0f;
		}
	}

	public abstract bool Emit( ParticleEffect target );
}
