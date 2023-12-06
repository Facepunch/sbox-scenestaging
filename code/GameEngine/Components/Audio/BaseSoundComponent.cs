using Sandbox;
using System;

public abstract class BaseSoundComponent : Component
{
	[Property, Group( "Sound" )] public SoundEvent SoundEvent { get; set; }
	[Property, Group( "Sound" )] public bool PlayOnStart { get; set; } = true;
	[Property, Group( "Sound" )] public bool StopOnNew { get; set; } = false;

	[Property, ToggleGroup( "SoundOverride" )] public bool SoundOverride { get; set; } = false;
	[Range( 0, 1 ), Property, Group( "SoundOverride" )] public float Volume { get; set; } = 1.0f;
	[Range( 0, 2 ), Property, Group( "SoundOverride" )] public float Pitch { get; set; } = 1.0f;
	[Property, Group( "SoundOverride" )] public bool Force2d { get; set; } = false;

	[Property, ToggleGroup( "Repeat" )] public bool Repeat { get; set; } = false;
	[Property, Group( "Repeat" )] public float MinRepeatTime { get; set; } = 1.0f;
	[Property, Group( "Repeat" )] public float MaxRepeatTime { get; set; } = 1.0f;

	protected SoundHandle SoundHandle;
	
	public virtual void StartSound() { }
	public virtual void StopSound() { }

}

