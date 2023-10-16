using Sandbox;
using Sandbox.Diagnostics;
using Sandbox.Services;
using Sandbox.UI;
using System;
using System.Threading;

[Title( "World Panel" )]
[Category( "UI" )]
[Icon( "light_mode", "red", "white" )]
[EditorHandle( "materials/gizmo/envmap.png" )]
// TODO needs parent component of type
public sealed class WorldPanel : BaseComponent, IPanelComponent
{
	[Property] public string PanelTypeName { get; set; }

	Sandbox.UI.WorldPanel worldPanel;

	[Property] public Vector2 PanelOffset { get; set; } = new Vector2( 0 );
	[Property] public Vector2 PanelSize { get; set; } = new Vector2( 512 );

	public override void DrawGizmos()
	{
		var technicalOffset = PanelOffset + new Vector2( PanelSize.x, PanelSize.y ) * 0.5f;

		Gizmo.Draw.LineBBox( BBox.FromPositionAndSize( new Vector3( 0, technicalOffset.x, -technicalOffset.y ) * Sandbox.ScenePanelObject.ScreenToWorldScale, new Vector3( 0, PanelSize.x, PanelSize.y ) * Sandbox.ScenePanelObject.ScreenToWorldScale ) );
	}

	public override void OnEnabled()
	{
		worldPanel = new Sandbox.UI.WorldPanel( Scene.SceneWorld );
		worldPanel.Transform = Transform.World;
	//	worldPanel.Style.BackgroundColor = Color.Cyan;
	}

	public override void OnDisabled()
	{
		Log.Info( "Destroy UI" );

		worldPanel?.Delete();
	}

	protected override void OnPreRender()
	{
		if ( worldPanel is null )
			return;

		worldPanel.Transform = Transform.World;
		worldPanel.PanelBounds = new Rect( PanelOffset, PanelSize );
	}

	public Panel GetPanel()
	{
		return worldPanel;
	}

}
