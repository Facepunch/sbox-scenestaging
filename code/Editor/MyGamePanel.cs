
using Editor;
using Editor.PanelInspector;
using Sandbox.Diagnostics;
using Sandbox.Internal;
using System.Linq;
using System;
using Sandbox;

[Dock( "Editor", "My Game Panel", "web_asset" )]
public partial class PanelInspectorWidget : Widget
{
	NativeRenderingWidget Renderer;
	SceneCamera CustomCamera;
	Gizmo.Instance SceneInstance;

	public PanelInspectorWidget( Widget parent ) : base( parent )
	{
		CustomCamera = new SceneCamera();

		Renderer = new NativeRenderingWidget( this );
		Renderer.Size = 200;

		Renderer.Camera = CustomCamera;

		Layout = Layout.Column();
		Layout.Add( Renderer );

		SceneInstance = new Gizmo.Instance();
		CustomCamera.Worlds.Add( SceneInstance.World );
	}

	[EditorEvent.Frame]
	public void Ticker()
	{
		CustomCamera.World = Camera.Main.World;
		CustomCamera.FieldOfView = 80;

		SceneInstance.FirstPersonCamera( CustomCamera, Renderer );
		SceneInstance.UpdateInputs( CustomCamera, Renderer );

		using ( SceneInstance.Push() )
		{
			Cursor = Gizmo.HasHovered ? CursorShape.Finger : CursorShape.Arrow;

			Gizmo.Draw.ScreenText( $"Scene Feet: [{Scene.Active}]", 10, flags: TextFlag.LeftCenter );

			if ( Scene.Active is not null )
			{
				float y = 30;
				foreach( var obj in Scene.Active.All)
				{
					var p = new Vector2( 10, y += 13.0f );

					var ray = CustomCamera.GetRay( (p + new Vector2( 120, 0 )) );
					var end = ray.Project( 10 );
					var dir = (obj.Transform.Position - ray.Project( 2000 )).Normal;

					Gizmo.Draw.Color = Color.Red;
					Gizmo.Draw.Line( obj.Transform.Position, end );
					Gizmo.Draw.SolidCone( obj.Transform.Position - dir * 10, dir * 10, 2.0f );

					Gizmo.Draw.Color = Color.White;
					Gizmo.Draw.ScreenText( $"Obj: [{obj.Transform.Position.x:n0}, {obj.Transform.Position.y:n0}, {obj.Transform.Position.z:n0}]", p, flags: TextFlag.LeftCenter );
				}
			}
		}
	}
}

