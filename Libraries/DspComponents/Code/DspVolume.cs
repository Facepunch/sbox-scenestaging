using Sandbox;
using Sandbox.Audio;

[Title( "Dsp Volume" )]
[Group( "Audio" )]
public class DspVolume : Sandbox.Volumes.VolumeComponent
{
	[Property]
	public DspPresetHandle Dsp { get; set; }

	[Property]
	public MixerHandle TargetMixer { get; set; } = new MixerHandle { Name = "Game" };

	[Property]
	public int Priority { get; set; }


	protected override void OnEnabled()
	{
		base.OnEnabled();

		Scene.GetSystem<DspVolumeGameSystem>().Add( this );
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();

		Scene.GetSystem<DspVolumeGameSystem>().Remove( this );
	}

	/// <summary>
	/// Prefer higher priority volumes, and if priorities are the same, we prefer smaller volumes.
	/// </summary>
	public override float GetPriority()
	{
		return Priority + base.GetPriority();
	}
}

