
internal class ScreenGizmoText : Component
{

	protected override void DrawGizmos()
	{
		base.DrawGizmos();

		Gizmo.Draw.ScreenText( "VELOCITY\t\t\t320\t\t\t\t0\nORIGIN\t\t\t\t88,64,254\t\t1\nANGLES\t\t\t\t0,90,0\t\t\t2", new( 32, 32 ), size: 15, flags: TextFlag.LeftTop );
	}

}
