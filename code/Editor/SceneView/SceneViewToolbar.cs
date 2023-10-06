public class SceneViewToolbar : SceneToolbar
{
	public SceneViewToolbar( Widget parent) : base( parent )
	{

	}

	public override void BuildToolbar()
	{
		AddGizmoModes();
		AddSeparator();
		AddCameraDropdown();
		AddSeparator();
		AddAdvancedDropdown();
		
	}
}
