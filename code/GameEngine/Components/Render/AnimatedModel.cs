using Sandbox;
using Sandbox.Diagnostics;
using System;
using System.Collections.Generic;

[Title( "Animated Model Renderer" )]
[Category( "Rendering" )]
[Icon( "sports_martial_arts" )]
public sealed partial class AnimatedModelComponent : BaseComponent, BaseComponent.ExecuteInEditor
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
				BuildBoneHeirarchy( GameObject );
			}
		}
	}

	string _materialGroup;

	[Property]
	public string MaterialGroup
	{
		get => _materialGroup;
		set
		{
			if ( _materialGroup == value ) return;

			_materialGroup = value;
			_sceneObject?.SetMaterialGroup( _materialGroup );
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

	bool _createBones = false;
	[Property]
	public bool CreateBoneObjects
	{
		get => _createBones;
		set
		{
			if ( _createBones == value ) return;
			_createBones = value;

			BuildBoneHeirarchy( GameObject );
		}
	}


	AnimatedModelComponent _boneMergeTarget;

	[Property]
	public AnimatedModelComponent BoneMergeTarget
	{
		get => _boneMergeTarget;
		set
		{
			if ( _boneMergeTarget == value ) return;

			_boneMergeTarget?.SetBoneMerge( this, false );

			_boneMergeTarget = value;

			_boneMergeTarget?.SetBoneMerge( this, true );
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


	public string TestString { get; set; }

	SceneModel _sceneObject;
	public SceneModel SceneObject => _sceneObject;


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

	HashSet<AnimatedModelComponent> mergeChildren = new ();

	private void SetBoneMerge( AnimatedModelComponent target, bool enabled )
	{
		ArgumentNullException.ThrowIfNull( target );

		if ( enabled )
		{
			mergeChildren.Add( target );
		}
		else
		{
			mergeChildren.Remove( target );
		}
	}


	public override void OnEnabled()
	{
		Assert.True( _sceneObject == null );
		Assert.NotNull( Scene );

		var model = Model ?? Model.Load( "models/dev/box.vmdl" );

		_sceneObject = new SceneModel( Scene.SceneWorld, model, Transform.World );
		_sceneObject.SetMaterialOverride( MaterialOverride );
		_sceneObject.ColorTint = Tint;
		_sceneObject.Flags.CastShadows = _castShadows;
		_sceneObject?.SetMaterialGroup( _materialGroup );
		_sceneObject.MeshGroupMask = _bodyGroupsMask;
		_sceneObject.Update( 0.01f );
		_sceneObject.OnFootstepEvent += InternalOnFootstep;
		_sceneObject.Tags.SetFrom( GameObject.Tags );

		_boneMergeTarget?.SetBoneMerge( this, true );

		BuildBoneHeirarchy( GameObject );
	}



	public override void OnDisabled()
	{
		ClearBoneProxies();

		_sceneObject?.Delete();
		_sceneObject = null;
	}

	public void UpdateInThread()
	{
		AnimationUpdate();
	}

	public void PostAnimationUpdate()
	{
		ThreadSafe.AssertIsMainThread();

		if ( !_sceneObject.IsValid() )
			return;

		_sceneObject.RunPendingEvents();
	}

	void AnimationUpdate()
	{
		if ( !_sceneObject.IsValid() )
			return;

		if ( _boneMergeTarget is not null )
			return;

		_sceneObject.Transform = Transform.World;

		if ( Scene.IsEditor )
		{
			_sceneObject.UpdateToBindPose();
		}
		else
		{
			_sceneObject.Update( Scene.IsEditor ? 0.0f : Time.Delta );
		}		

		MergeChildren();
	}

	void MergeChildren()
	{
		foreach ( var child in mergeChildren )
		{
			if ( child.SceneObject is null )
				continue;

			child.SceneObject.Transform = Transform.World;
			child.SceneObject.MergeBones( SceneObject );
		}
	}

	public override void Update()
	{
		if ( Scene.ThreadedAnimation )
			return;

		AnimationUpdate();
		PostAnimationUpdate();
	}

	/// <summary>
	/// Called when a footstep event happens
	/// </summary>
	public Action<SceneModel.FootstepEvent> OnFootstepEvent { get; set; }

	private void InternalOnFootstep( SceneModel.FootstepEvent e )
	{
		OnFootstepEvent?.Invoke( e );
	}
	
	public Transform? GetAttachment( string name, bool worldSpace = true )
	{
		return _sceneObject?.GetAttachment( name, worldSpace );
	}
}
