
internal class ScreenGizmoText : Component
{

	protected override void DrawGizmos()
	{
		base.DrawGizmos();

		Gizmo.Draw.ScreenText( "Tab test:\t1\t\t\tColumn\nTab test:\t2\t\t\tColumn", new( 64, 64 ), size: 20, flags: TextFlag.LeftCenter );
	}

}
