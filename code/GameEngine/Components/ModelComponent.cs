using Sandbox;
using Sandbox.Diagnostics;

[Title( "Model Renderer" )]
[Category( "Rendering" )]
[Icon( "visibility", "red", "white" )]
public class ModelComponentMate : GameObjectComponent
{
	[Property] public Model Model { get; set; }


	Color _tint = Color.White;

	[Property]
	public Color Tint
	{
		get => _tint;
		set
		{
			if ( _tint == value ) return;

			_tint = value;

			if ( _sceneObject is not null )
			{
				_sceneObject.ColorTint = Tint;
			}
		}
	}

	[Property] public Material MaterialOverride { get; set; }

	public string TestString { get; set; }

	SceneObject _sceneObject;
	public SceneObject SceneObject => _sceneObject;


	public override void DrawGizmos()
	{
		if ( Model is not null )
		{
			_sceneObject = Gizmo.Draw.Model( Model, Transform.Zero );
		}
		else
		{
			_sceneObject = Gizmo.Draw.Model( "models/dev/box.vmdl", Transform.Zero );
		}

		_sceneObject.ColorTint = Tint;
		_sceneObject.SetMaterialOverride( MaterialOverride );

		var bounds = _sceneObject.Model.Bounds;

		// todo - hitbox.model()
		Gizmo.Hitbox.BBox( bounds );

		if ( Gizmo.IsHovered )
		{
			Gizmo.Draw.LineBBox( bounds );
		}

		if ( Gizmo.IsSelected )
		{
			Gizmo.Draw.LineBBox( bounds );
		}

		//Gizmo.Draw.LineSphere( new Sphere( PunchLocation, 1 ) );
	}

	public override void OnEnabled()
	{
		Assert.True( _sceneObject == null );

		_sceneObject = new SceneObject( Camera.Main.World, Model );
		_sceneObject.Transform = GameObject.WorldTransform;
		_sceneObject.SetMaterialOverride( MaterialOverride );
		_sceneObject.ColorTint = Tint;
	}

	public override void OnDisabled()
	{
		_sceneObject?.Delete();
		_sceneObject = null;
	}

	protected override void OnPreRender()
	{
		_sceneObject.Transform = GameObject.WorldTransform;
	}

}
