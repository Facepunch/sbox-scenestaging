using Sandbox;

[Category( "Audio" )]
[Title( "Sound Point" )]
[Icon( "volume_up", "red", "white" )]
[EditorHandle( "materials/gizmo/sound.png" )]
public sealed class SoundPointComponent : BaseSoundComponent
{  
	protected override void OnEnabled()
	{
		if ( PlayOnStart )
		{
			StartSound();
		}
	}

	TimeUntil TimeUntilRepeat;

	public override void StartSound()
	{
		var source = GameObject;

		if ( StopOnNew )
		{
			SoundHandle.Stop( false );
			SoundHandle = default;
		}

		if ( SoundHandle.IsPlaying ) return;

		SoundHandle = Audio.Play( SoundEvent );
		SoundHandle.Position = source.Transform.Position;

		ApplyOverrides();

		TimeUntilRepeat = Random.Shared.Float( MinRepeatTime, MaxRepeatTime );
	}

	void ApplyOverrides()
	{
		if ( SoundOverride )
		{
			SoundHandle.Volume = Volume;
			SoundHandle.Pitch = Pitch;

			if ( Force2d )
				SoundHandle.Position = Sound.Listener.Value.Position + Sound.Listener.Value.Rotation.Forward * 10.0f;
		}
	}

	public override void StopSound()
	{
		SoundHandle.Stop( false );
	}

	protected override void OnUpdate()
	{
		SoundHandle.Position = Transform.Position;

		ApplyOverrides();

		if ( Repeat && TimeUntilRepeat <= 0.0f )
		{
			StartSound();
		}
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		StopSound();
	}
}
