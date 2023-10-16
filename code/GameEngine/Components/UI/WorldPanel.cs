using Editor;
using Sandbox;
using Sandbox.Diagnostics;
using Sandbox.Services;
using Sandbox.UI;
using System;
using System.Threading;

[Title( "World Panel" )]
[Category( "UI" )]
[Icon( "panorama_horizontal" )]
[EditorHandle( "materials/gizmo/ui.png" )]
// TODO needs parent component of type
public sealed class WorldPanel : BaseComponent, IRootPanelComponent
{
	Sandbox.UI.WorldPanel worldPanel;

	[Property] public float RenderScale { get; set; } = 1.0f;
	[Property] public bool LookAtCamera { get; set; }
	[Property] public Vector2 PanelSize { get; set; } = new Vector2( 512 );
	[Property] public HAlignment HorizontalAlign { get; set; } = HAlignment.Center;
	[Property] public VAlignment VerticalAlign { get; set; } = VAlignment.Center;


	public enum HAlignment
	{
		Left = 1,
		Center = 2,
		Right = 3,
	}

	public enum VAlignment
	{
		Top = 1,
		Center = 2,
		Bottom = 3,
	}

	Rect CalculateRect()
	{
		var r = new Rect( 0, PanelSize );

		if ( HorizontalAlign == HAlignment.Center ) r.Position -= new Vector2( PanelSize.x * 0.5f, 0 );
		if ( HorizontalAlign == HAlignment.Right ) r.Position -= new Vector2( PanelSize.x, 0 );

		if ( VerticalAlign == VAlignment.Center ) r.Position -= new Vector2( 0, PanelSize.y * 0.5f);
		if ( VerticalAlign == VAlignment.Bottom ) r.Position -= new Vector2( 0, PanelSize.y );


		return r;
	}


	public override void DrawGizmos()
	{
		using ( Gizmo.Scope( null, new Transform( 0, Rotation.From( 0, 90, -90 ), Gizmo.Transform.Scale * Sandbox.ScenePanelObject.ScreenToWorldScale ) ) )
		{
			var r = CalculateRect();

			Gizmo.Draw.Line( r.TopLeft, r.TopRight );
			Gizmo.Draw.Line( r.TopLeft, r.BottomLeft );
			Gizmo.Draw.Line( r.TopRight, r.BottomRight );
			Gizmo.Draw.Line( r.BottomLeft, r.BottomRight );

			Gizmo.Draw.Color = Color.Cyan.WithAlpha( 0.2f );
			Gizmo.Draw.SolidTriangle( new Triangle( r.TopLeft, r.TopRight, r.BottomRight ) );
			Gizmo.Draw.SolidTriangle( new Triangle( r.BottomRight, r.BottomLeft, r.TopLeft ) );
		}
	}

	public override void OnEnabled()
	{
		worldPanel = new Sandbox.UI.WorldPanel( Scene.SceneWorld );
		worldPanel.Transform = Transform.World;
	}

	public override void OnDisabled()
	{
		worldPanel?.Delete();
	}

	protected override void OnPreRender()
	{
		if ( worldPanel is null )
			return;

		var currentRot = Transform.World.Rotation;
		var currentScale = Transform.World.Scale;

		if ( LookAtCamera )
		{
			var camPos = Camera.Main.Position; // TODO: CameraComponent.Current / Main?
			var camDelta = camPos - Transform.World.Position;
			currentRot = Rotation.LookAt( camDelta, Camera.Main.Rotation.Up );
		}

		worldPanel.Transform = Transform.World.WithRotation( currentRot ).WithScale( currentScale * RenderScale);

		var rect = CalculateRect();

		rect.Left /= RenderScale;
		rect.Right /= RenderScale;
		rect.Top /= RenderScale;
		rect.Bottom /= RenderScale;

		worldPanel.PanelBounds = rect;
	}

	public Panel GetPanel()
	{
		return worldPanel;
	}

}
