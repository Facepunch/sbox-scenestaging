using Sandbox;

[Category( "Audio" )]
[Title( "Sound Listener" )]
[Icon( "volume_down", "red", "white" )]
[EditorHandle( "materials/gizmo/audiolistener.png" )]
public sealed class SoundListenerComponent : BaseComponent
{
	public override void Update()
	{
		Sound.Listener = new()
		{
			Position = Transform.Position,
			Rotation = Transform.Rotation
		};
	}
}
