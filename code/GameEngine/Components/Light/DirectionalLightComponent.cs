using Sandbox;
using Sandbox.Diagnostics;
using System;

[Title( "Directional Light" )]
[Category( "Light" )]
[Icon( "light_mode", "red", "white" )]
[EditorHandle( "materials/gizmo/directionallight.png" )]
public class DirectionalLightComponent : BaseComponent, IComponentColorProvider, BaseComponent.ExecuteInEditor
{
	SceneSunLight _sceneObject;

	Color _lightColor = "#E9FAFF";
	[Property] public Color LightColor
	{
		get => _lightColor;
		set
		{
			if ( _lightColor == value ) return;
			_lightColor = value;

			if ( _sceneObject.IsValid() )
			{
				_sceneObject.LightColor = LightColor;
			}
		}
	}

	Color _skyColor = "#0F1315";
	[Property] public Color SkyColor 
	{
		get => _skyColor;
		set
		{
			if ( _skyColor  == value ) return;
			_skyColor = value;

			if ( _sceneObject.IsValid() )
			{
				_sceneObject.SkyColor = _skyColor;
			}
		}
	}

	Color IComponentColorProvider.ComponentColor => LightColor;

	[Property] public bool Shadows { get; set; } = true;

	public override void DrawGizmos()
	{
		using var scope = Gizmo.Scope( $"light-{GetHashCode()}" );

		var fwd = Vector3.Forward;

		Gizmo.Draw.Color = LightColor;

		for ( float f = 0; f<MathF.PI * 2; f+= 0.5f  )
		{
			var x = MathF.Sin( f );
			var y = MathF.Cos( f );

			var off = (x * Vector3.Left + y * Vector3.Up) * 5.0f;

			Gizmo.Draw.Line( off, off + fwd * 30 );
		}

	//	Gizmo.Transform = Transform.Zero;
	//	Gizmo.Draw.Sprite( GameObject.Transform.Position, 10, Texture.Load( FileSystem.Mounted, "/editor/directional_light.png" ) );

	}

	public override void OnEnabled()
	{
		Assert.True( _sceneObject == null );
		Assert.NotNull( Scene );

		_sceneObject = new SceneSunLight( Scene.SceneWorld, Transform.Rotation, Color.White );
		_sceneObject.Transform = Transform.World;
		_sceneObject.ShadowsEnabled = true;
		_sceneObject.ShadowCascadeCount = 3;
		_sceneObject.LightColor = LightColor;
		_sceneObject.SkyColor = SkyColor;

		// garry: I dunno if any of this works, just trying to make them look less shit
		_sceneObject.SetShadowCascadeResolution( 0, 1024 * 2 );
		_sceneObject.SetShadowCascadeResolution( 1, 1024 * 4 );
		_sceneObject.SetShadowCascadeResolution( 2, 1024 * 8 );

		_sceneObject.SetShadowCascadeDistance( 0, 300 );
		_sceneObject.SetShadowCascadeDistance( 1, 600 );
		_sceneObject.SetShadowCascadeDistance( 2, 4000 );
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

		_sceneObject.Transform = Transform.World.WithScale( 1 );
		_sceneObject.ShadowsEnabled = Shadows;



	}

}
