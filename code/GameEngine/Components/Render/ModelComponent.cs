using Sandbox;
using Sandbox.Diagnostics;

[Title( "Model Renderer" )]
[Category( "Rendering" )]
[Icon( "free_breakfast" )]
[Alias( "ModelComponentMate" )]
public class ModelComponent : BaseComponent, BaseComponent.ExecuteInEditor, BaseComponent.ITintable
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

			return new BBox( Transform.Position, 16 );
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

	bool _castShadows = true;
	[Property]
	public bool ShouldCastShadows
	{
		get => _castShadows;
		set
		{
			if ( _castShadows == value ) return;
			_castShadows = value;

			if ( _sceneObject is not null )
			{
				_sceneObject.Flags.CastShadows = _castShadows;
			}
		}
	}

	ulong _bodyGroupsMask = ulong.MaxValue;
	[Property, Model.BodyGroupMask]
	public ulong BodyGroups
	{
		get => _bodyGroupsMask;
		set
		{
			if ( _bodyGroupsMask == value ) return;
			_bodyGroupsMask = value;

			if ( _sceneObject is not null )
			{
				_sceneObject.MeshGroupMask = _bodyGroupsMask;
			}
		}
	}

	SceneObject _sceneObject;
	public SceneObject SceneObject => _sceneObject;

	Color ITintable.Color { get => Tint; set => Tint = value; }

	public override void DrawGizmos()
	{
		if ( Model is null )
			return;

		Gizmo.Hitbox.Model( Model );

		Gizmo.Draw.Color = Color.White.WithAlpha( 0.1f );

		if ( Gizmo.IsSelected )
		{
			Gizmo.Draw.Color = Color.White.WithAlpha( 0.9f );
			Gizmo.Draw.LineBBox( Model.Bounds );
		}
		
		if ( Gizmo.IsHovered )
		{
			Gizmo.Draw.Color = Color.White.WithAlpha( 0.4f );
			Gizmo.Draw.LineBBox( Model.Bounds );
		}
	}

	public override void OnEnabled()
	{
		Assert.True( _sceneObject == null );
		Assert.NotNull( Scene );

		var model = Model ?? Model.Load( "models/dev/box.vmdl" );

		_sceneObject = new SceneObject( Scene.SceneWorld, model, Transform.World );
		_sceneObject.SetMaterialOverride( MaterialOverride );
		_sceneObject.ColorTint = Tint;
		_sceneObject.Flags.CastShadows = _castShadows;
		_sceneObject.MeshGroupMask = _bodyGroupsMask;
		_sceneObject.Tags.SetFrom( GameObject.Tags );
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
	}

}
