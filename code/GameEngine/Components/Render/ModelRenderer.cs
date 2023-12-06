using Sandbox;
using Sandbox.Diagnostics;

[Title( "Model Renderer" )]
[Category( "Rendering" )]
[Icon( "free_breakfast" )]
[Alias( "ModelComponentMate", "ModelComponent" )]
public class ModelRenderer : Renderer, Component.ExecuteInEditor, Component.ITintable
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
			UpdateObject();
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
			UpdateObject();
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
			UpdateObject();
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
			UpdateObject();
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
			UpdateObject();
		}
	}

	internal SceneObject _sceneObject;
	public SceneObject SceneObject => _sceneObject;

	Color ITintable.Color { get => Tint; set => Tint = value; }

	protected override void DrawGizmos()
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

	protected virtual void UpdateObject()
	{
		if ( !_sceneObject.IsValid() )
			return;

		_sceneObject.ColorTint = Tint;
		_sceneObject.Flags.CastShadows = _castShadows;
		_sceneObject.MeshGroupMask = _bodyGroupsMask;
		_sceneObject.Model = Model;
		_sceneObject.SetMaterialOverride( MaterialOverride );
	}

	protected override void OnEnabled()
	{
		Assert.True( _sceneObject == null );
		Assert.NotNull( Scene );

		var model = Model ?? Model.Load( "models/dev/box.vmdl" );

		_sceneObject = new SceneObject( Scene.SceneWorld, model, Transform.World );
		_sceneObject.Tags.SetFrom( GameObject.Tags );

		UpdateObject();
	}

	protected override void OnDisabled()
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

	/// <summary>
	/// Tags have been updated - lets update our scene object tags
	/// </summary>
	protected override void OnTagsChannged()
	{
		if ( !_sceneObject.IsValid() ) return;

		_sceneObject.Tags.SetFrom( GameObject.Tags );
	}

}
