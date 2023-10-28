using Sandbox;
using Sandbox.UI;

[Title( "Screen Panel" )]
[Category( "UI" )]
[Icon( "desktop_windows" )]
[EditorHandle( "materials/gizmo/ui.png" )]
[Alias( "PanelRoot" )]
public sealed class ScreenPanel : BaseComponent, IRootPanelComponent, BaseComponent.RenderOverlay
{
	[Property, Range( 0, 1 )] public float Opacity { get; set; } = 1.0f;
	[Property, Range( 0, 5 )] public float Scale { get; set; } = 1.0f;
	[Property] public bool AutoScreenScale { get; set; } = true;
	[Property] public int ZIndex { get; set; } = 100;

	private GameRootPanel rootPanel;

	public override void OnValidate()
	{
		if ( Scale < 0.001f ) Scale = 0.001f;
	}

	public override void OnAwake()
	{
		rootPanel = new GameRootPanel();
		rootPanel.RenderedManually = true;
		rootPanel.Style.Display = DisplayMode.None;
	}

	public override void OnEnabled()
	{
		if ( rootPanel is null )
			return;

		// todo RootPanel enable
		rootPanel.Style.Display = DisplayMode.Flex;
	}

	public override void OnDisabled()
	{
		// todo disable rootpanel
		if ( rootPanel is null )
			return;

		rootPanel.Style.Display = DisplayMode.None;
	}

	public override void OnDestroy()
	{
		rootPanel?.Delete();
		rootPanel = null;
	}

	public Panel GetPanel()
	{
		return rootPanel;
	}

	public override void Update()
	{
		if ( rootPanel is null )
			return;

		rootPanel.Style.ZIndex = ZIndex;
		rootPanel.AutoScale = AutoScreenScale;
		rootPanel.ManualScale = Scale;
	}

	void BaseComponent.RenderOverlay.OnRenderOverlay( SceneCamera camera )
	{
		if ( rootPanel is null ) return;
		if ( !camera.EnableUserInterface ) return;

		rootPanel.RenderManual( Opacity );
	}
}

class GameRootPanel : RootPanel
{
	public bool AutoScale = true;
	public float ManualScale;

	override public bool IsWorldPanel => false;

	protected override void UpdateScale( Rect screenSize )
	{
		if ( AutoScale )
		{
			base.UpdateScale( screenSize * ManualScale );
		}
		else
		{
			Scale = ManualScale;
		}
	}
}
