using Sandbox;
using Sandbox.Diagnostics;
using System;

[Title( "Point Light" )]
[Category( "Light" )]
[Icon( "light_mode", "red", "white" )]
[EditorHandle( "materials/gizmo/pointlight.png" )]
public class PointLightComponent : BaseComponent, IComponentColorProvider, BaseComponent.ExecuteInEditor, BaseComponent.ITintable
{
	SceneLight _sceneObject;

	[Property] public Color LightColor { get; set; } = "#E9FAFF";
	[Property] public float Radius { get; set; } = 400;

	Color IComponentColorProvider.ComponentColor => LightColor;

	Color ITintable.Color { get => LightColor; set => LightColor = value; }

	public override void DrawGizmos()
	{
		using var scope = Gizmo.Scope( $"light-{GetHashCode()}" );

		if ( Gizmo.IsSelected )
		{
			Gizmo.Draw.Color = LightColor.WithAlpha( 0.9f );
			Gizmo.Draw.LineSphere( new Sphere( Vector3.Zero, Radius ), 12 );
		}

		if ( Gizmo.IsHovered )
		{
			Gizmo.Draw.Color = LightColor.WithAlpha( 0.4f );
			Gizmo.Draw.LineSphere( new Sphere( Vector3.Zero, Radius ), 12 );
		}
	}

	public override void OnEnabled()
	{
		Assert.True( _sceneObject == null );
		Assert.NotNull( Scene );

		_sceneObject = new SceneLight( Scene.SceneWorld, Transform.Position, Radius, LightColor );
		_sceneObject.FogLighting = SceneLight.FogLightingMode.Dynamic;
	}

	public override void OnDisabled()
	{
		_sceneObject?.Delete();
		_sceneObject = null;
	}

	protected override void OnPreRender()
	{
		if ( !_sceneObject.IsValid() )
			return;

		_sceneObject.Transform = Transform.World;
		_sceneObject.LightColor = LightColor;
		_sceneObject.Radius = Radius;
	}

}
