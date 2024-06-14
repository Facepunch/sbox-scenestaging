namespace Sandbox.Volumes;

public class VolumeComponent : Component
{
	[InlineEditor]
	[Property] public SceneVolume SceneVolume { get; set; } = new SceneVolume();

	protected override void DrawGizmos()
	{
		base.DrawGizmos();

		if ( !Gizmo.IsSelected )
			return;

		var vol = SceneVolume;
		vol.DrawGizmos( true );
		SceneVolume = vol;
	}

	public virtual float GetPriority()
	{
		// higher number is better, smaller volume is better
		return 1.0f - (SceneVolume.GetVolume() / 16000000000f);
	}
}
