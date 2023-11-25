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

		// Spacer

		{
			var o = new Option( "Lighting", "light_mode" );
			o.Checkable = true;
			o.Checked = true;// !(SceneInstance?.GetValue<bool>( "unlit" ) ?? false );
			o.Toggled = v => SceneInstance.SetValue( "unlit", !v );
			AddOption( o );
		}

	}
}
