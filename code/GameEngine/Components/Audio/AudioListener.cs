using Sandbox;

[Category( "Audio" )]
[Title( "Listener" )]
[Icon( "hearing" )]
[EditorHandle( "materials/gizmo/audiolistener.png" )]
[Alias( "SoundListener" )]
public sealed class AudioListener : Component
{
	protected override void OnUpdate()
	{
		Sound.Listener = Transform.World.WithScale( 1 );
	}

	protected override void OnPreRender()
	{
		Sound.Listener = Transform.World.WithScale( 1 );
	}
}
