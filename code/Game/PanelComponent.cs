using Sandbox;
using Sandbox.Diagnostics;
using Sandbox.Services;
using Sandbox.UI;
using System;
using System.Threading;

public sealed class PanelComponent : BaseComponent
{
	RootPanel rootPanel;

	public override void OnEnabled()
	{
		Log.Info( "Create UI" );
		rootPanel = new GameScreen();
		rootPanel.RenderedManually = true;

		Camera.Main.OnRenderOverlay += RenderPanel;
	}

	private void RenderPanel()
	{
		rootPanel.RenderManual();
	}

	public override void OnDisabled()
	{
		Log.Info( "Destroy UI" );
		Camera.Main.OnRenderOverlay -= RenderPanel;
		rootPanel.Delete();
		rootPanel = null;
	}

	protected override void OnPreRender()
	{
		
	}
}
