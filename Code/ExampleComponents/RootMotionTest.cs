
public sealed class RootMotionTest : Component
{
	[Property]
	public SkinnedModelRenderer Renderer { get; set; }

	[Property, Range( -300, 300 )] 
	public float MoveX { get; set; } = 10;

	protected override void OnUpdate()
	{
		if ( Renderer.IsValid() )
		{
			Renderer.Set( "move_x", MoveX );
			WorldPosition += Renderer.RootMotion.Position;

			if ( WorldPosition.x > 100 || WorldPosition.x < -100 )
				WorldPosition = 0;
		}
	}
}
