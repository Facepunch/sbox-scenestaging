using Sandbox;
using Sandbox.Diagnostics;

[Title( "2D Skybox" )]
[Category( "Rendering" )]
[Icon( "visibility", "red", "white" )]
[EditorHandle( "materials/gizmo/2dskybox.png" )]
public class SkyBox2D : BaseComponent, BaseComponent.ExecuteInEditor
{
	Color _tint = Color.White;

	[Property]
	public Color Tint
	{
		get => _tint;
		set
		{
			if ( _tint == value ) return;

			_tint = value;

			if ( sceneObject is not null )
			{
				sceneObject.SkyTint = Tint;
			}
		}
	}

	Material _material = Material.Load( "materials/skybox/light_test_sky_sunny02.vmat" ); // todo - better default

	[Property] public Material SkyMaterial
	{
		get => _material;
		set
		{
			if ( _material == value ) return;

			_material = value;

			if ( sceneObject is not null )
			{
				sceneObject.SkyMaterial = _material;
			}
		}
	}

	SceneSkyBox sceneObject;

	public override void OnEnabled()
	{
		Assert.True( sceneObject == null );
		Assert.NotNull( Scene );

		sceneObject = new SceneSkyBox( Scene.SceneWorld, SkyMaterial );
		sceneObject.SkyTint = Tint;
		sceneObject.Tags.SetFrom( GameObject.Tags );
	}

	public override void OnDisabled()
	{
		sceneObject?.Delete();
		sceneObject = null;
	}

	protected override void OnPreRender()
	{
		if ( !sceneObject.IsValid() )
			return;

		sceneObject.Transform = Transform.World;
	}

}
