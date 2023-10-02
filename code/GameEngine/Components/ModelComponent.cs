using Sandbox;
using Sandbox.Diagnostics;

[Title( "Model Renderer" )]
[Category( "Rendering" )]
[Icon( "visibility", "red", "white" )]
[Alias( "ModelComponentMate" )]
public class ModelComponent : GameObjectComponent
{
	Model _model;

	public BBox Bounds
	{
		get
		{
			if ( _sceneObject is not null )
			{
				return _sceneObject.Bounds;
			}

			return new BBox( GameObject.WorldTransform.Position, 16 );
		}
	}

	[Property] public Model Model 
	{
		get => _model;
		set
		{
			if ( _model == value ) return;
			_model = value;

			if ( _sceneObject is not null )
			{
				_sceneObject.Model = _model;
			}
		}
	}


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

	Material _material;
	[Property] public Material MaterialOverride
	{
		get => _material;
		set
		{
			if ( _material == value ) return;
			_material = value;

			if ( _sceneObject is not null )
			{
				_sceneObject.SetMaterialOverride( _material );
			}
		}
	}

	public string TestString { get; set; }

	SceneObject _sceneObject;
	public SceneObject SceneObject => _sceneObject;


	public override void DrawGizmos()
	{
		if ( Model is null )
			return;

		var bounds = Model.Bounds;

		// todo - hitbox.model()
		Gizmo.Hitbox.BBox( bounds );

		Gizmo.Draw.Color = Color.White.WithAlpha( 0.1f );

		if ( Gizmo.IsSelected )
		{
			Gizmo.Draw.Color = Color.White.WithAlpha( 0.9f );
			Gizmo.Draw.LineBBox( bounds );
		}
		
		if ( Gizmo.IsHovered )
		{
			Gizmo.Draw.Color = Color.White.WithAlpha( 0.4f );
			Gizmo.Draw.LineBBox( bounds );
		}



		
	}

	public override void OnEnabled()
	{
		Assert.True( _sceneObject == null );
		Assert.NotNull( Scene );

		_sceneObject = new SceneObject( Scene.SceneWorld, Model ?? Model.Load( "models/dev/box.vmdl" ) );
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
		if ( !_sceneObject.IsValid() )
			return;

		_sceneObject.Transform = GameObject.WorldTransform;
	}

}
