using Sandbox;
using Sandbox.UI;

[Title( "Panel" )]
[Category( "UI" )]
[Icon( "widgets" )]
public sealed class PanelHost : BaseComponent, IPanelComponent
{
	// Todo class selector, by base?
	// maybe we have a special GamePanel base that people derive from to show in this list?
	[Property] public string PanelTypeName { get; set; }

	Panel panel;

	public override void OnEnabled()
	{
		panel = TypeLibrary.Create<Panel>( PanelTypeName );
		UpdateParent();
	}

	public override void OnStart()
	{
		UpdateParent();
	}

	void UpdateParent()
	{
		if ( panel is null ) return;

		var root = GameObject.GetComponentInParent<IPanelComponent>( false );
		panel.Parent = root?.GetPanel();
	}

	public override void OnDisabled()
	{
		panel?.Delete();
		panel = null;
	}

	public Panel GetPanel()
	{
		return panel;
	}
}

interface IPanelComponent
{
	Panel GetPanel();
}
