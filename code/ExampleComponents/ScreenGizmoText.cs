
internal class ScreenGizmoText : Component
{

	protected override void DrawGizmos()
	{
		base.DrawGizmos();

		Gizmo.Draw.ScreenText( "Tab testt:\t1\t\tColumn\nTab test:\t2", new( 64, 64 ), size: 20, flags: TextFlag.LeftCenter );
		Gizmo.Draw.ScreenText( "Tab   test:\t3\t\tColumn", new( 64, 110 ), size: 20, flags: TextFlag.LeftCenter );
	}

}
