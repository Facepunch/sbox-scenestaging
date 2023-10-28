using Sandbox;
using Sandbox.Diagnostics;
using System;

[Title( "Spot Light" )]
[Category( "Light" )]
[Icon( "light_mode", "red", "white" )]
[EditorHandle( "materials/gizmo/spotlight.png" )]
public class SpotLightComponent : BaseComponent, IComponentColorProvider, BaseComponent.ExecuteInEditor, BaseComponent.ITintable
{
	SceneSpotLight _sceneObject;

	[Property] public Color LightColor { get; set; } = "#E9FAFF";
	[Property] public float Radius { get; set; } = 500;

	[Range(0, 90)]
	[Property] public float ConeOuter { get; set; } = 45;

	[Range( 0, 90 )]
	[Property] public float ConeInner { get; set; } = 15;

	[Range( 0, 10 )]
	[Property] public float Attenuation { get; set; } = 1.0f;
	[Property] public Texture Cookie { get; set; }

	Color IComponentColorProvider.ComponentColor => LightColor;

	Color ITintable.Color { get => LightColor; set => LightColor = value; }

	public override void DrawGizmos()
	{
		using var scope = Gizmo.Scope( $"light-{GetHashCode()}" );

		if ( !Gizmo.IsSelected && !Gizmo.IsHovered )
			return;

		Gizmo.Draw.Color = LightColor.WithAlpha( Gizmo.IsSelected ? 0.9f : 0.4f );

		var fwd = Vector3.Forward;

		// todo: the inner cone is some weird shit with falloff
		var outerRadius = Radius * MathF.Tan( ConeOuter.DegreeToRadian() );
		var sections = 16;

		for ( int i = 0; i <= sections; i++ )
		{
			var f = ((float)i + 1) / sections * MathF.PI * 2;
			Vector3 vPos = Vector3.Zero;
			vPos += Vector3.Left * outerRadius * MathF.Sin( f );
			vPos += Vector3.Up * outerRadius * MathF.Cos( f );
			vPos += fwd * Radius;

			Gizmo.Draw.Line( Vector3.Zero, vPos );
		}

		Gizmo.Draw.LineCircle( fwd * Radius, outerRadius, sections: sections );
	}

	public override void OnEnabled()
	{
		Assert.True( _sceneObject == null );
		Assert.NotNull( Scene );

		_sceneObject = new SceneSpotLight( Scene.SceneWorld, Transform.Position, LightColor );
		//_sceneObject.Transform = GameObject.WorldTransform;
		//_sceneObject.ShadowsEnabled = true;

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
	//	_sceneObject.ShadowsEnabled = Shadows;
		_sceneObject.LightColor = LightColor;
		_sceneObject.FallOff = 1;
		_sceneObject.Radius = Radius;
		_sceneObject.ConeInner = ConeInner;
		_sceneObject.ConeOuter = ConeOuter;
		_sceneObject.QuadraticAttenuation = Attenuation;
		_sceneObject.LightCookie = Cookie;
		_sceneObject.ShadowTextureResolution = 4096;
		////_sceneObject.FallOff = 0.1f;
		//_sceneObject.SkyColor = SkyColor;

		//_sceneObject.ShadowCascadeCount = 3;
		//_sceneObject.SetShadowCascadeDistance( 0, 200 );
		//_sceneObject.SetShadowCascadeDistance( 1, 500 );
		//_sceneObject.SetShadowCascadeDistance( 2, 5000 );
	}

}
