using Sandbox;
using Sandbox.Diagnostics;
using System;

[Title( "Spot Light" )]
[Category( "Light" )]
[Icon( "light_mode", "red", "white" )]
public class SpotLightComponent : GameObjectComponent
{
	SceneSpotLight _sceneObject;

	[Property] public Color LightColor { get; set; } = "#E9FAFF";
	[Property] public float Radius { get; set; } = 500;
	[Property] public float ConeOuter { get; set; } = 45;
	[Property] public float ConeInner { get; set; } = 15;
	[Property] public float Attenuation { get; set; } = 1.0f;
	[Property] public Texture Cookie { get; set; }

	public override void DrawGizmos()
	{
		using var scope = Gizmo.Scope( $"light-{GetHashCode()}" );

		var fwd = Vector3.Forward;

		Gizmo.Draw.Color = LightColor;

	//	Gizmo.Transform = Transform.Zero;
	//	Gizmo.Draw.Sprite( GameObject.Transform.Position, 10, Texture.Load( FileSystem.Mounted, "/editor/directional_light.png" ) );

	}

	public override void OnEnabled()
	{
		Assert.True( _sceneObject == null );
		Assert.NotNull( Scene );

		_sceneObject = new SceneSpotLight( Scene.SceneWorld, GameObject.Transform.Position, LightColor );
		//_sceneObject.Transform = GameObject.WorldTransform;
		//_sceneObject.ShadowsEnabled = true;
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

		_sceneObject.Transform = GameObject.Transform;
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
