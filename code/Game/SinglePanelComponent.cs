using Sandbox;
using Sandbox.Diagnostics;
using Sandbox.Services;
using Sandbox.UI;
using System;
using System.Threading;

public sealed class SinglePanelComponent : BaseComponent
{
	Panel panel;

	public override void OnEnabled()
	{
		Log.Info( "Create UI" );
		panel = new Panel();

		Camera.Main.OnRenderOverlay += RenderPanel;
	}

	private void RenderPanel()
	{
		
	}

	public override void OnDisabled()
	{
		Log.Info( "Destroy UI" );
		Camera.Main.OnRenderOverlay -= RenderPanel;

		panel.Delete();
		panel = null;
	}

	protected override void OnPreRender()
	{
		
	}
}
