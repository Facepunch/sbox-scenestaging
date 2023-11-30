using Sandbox;
using Sandbox.Diagnostics;
using System;
using System.Collections.Generic;

[Title( "Model Renderer (skinned)" )]
[Category( "Rendering" )]
[Icon( "sports_martial_arts" )]
[Alias( "AnimatedModelComponent" )]
public sealed partial class SkinnedModelRenderer : ModelRenderer
{
	bool _createBones = false;

	[Property, Group( "Bones" )]
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

	SkinnedModelRenderer _boneMergeTarget;

	[Property, Group( "Bones" )]
	public SkinnedModelRenderer BoneMergeTarget
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


	public SceneModel SceneModel => (SceneModel) _sceneObject;

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

	HashSet<SkinnedModelRenderer> mergeChildren = new ();

	private void SetBoneMerge( SkinnedModelRenderer target, bool enabled )
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


	protected override void OnEnabled()
	{
		Assert.True( _sceneObject == null );
		Assert.NotNull( Scene );

		var model = Model ?? Model.Load( "models/dev/box.vmdl" );

		var so = new SceneModel( Scene.SceneWorld, model, Transform.World );
		so.Tags.SetFrom( GameObject.Tags );
		_sceneObject = so;

		UpdateObject();

		so.Update( 0.01f );

		_boneMergeTarget?.SetBoneMerge( this, true );
		BuildBoneHeirarchy( GameObject );
	}

	protected override void UpdateObject()
	{
		base.UpdateObject();

		if ( !SceneModel.IsValid() )
			return;

		SceneModel.OnFootstepEvent = InternalOnFootstep;
	}


	protected override void OnDisabled()
	{
		ClearBoneProxies();

		_sceneObject?.Delete();
		_sceneObject = null;
	}



	public void PostAnimationUpdate()
	{
		ThreadSafe.AssertIsMainThread();

		if ( !SceneModel.IsValid() )
			return;

		SceneModel.RunPendingEvents();
	}

	internal void AnimationUpdate()
	{
		if ( !SceneModel.IsValid() )
			return;

		if ( _boneMergeTarget is not null )
			return;

		_sceneObject.Transform = Transform.World;

		if ( Scene.IsEditor )
		{
			SceneModel.UpdateToBindPose();
		}
		else
		{
			SceneModel.Update( Scene.IsEditor ? 0.0f : Time.Delta );
		}		

		MergeChildren();
	}

	void MergeChildren()
	{
		foreach ( var child in mergeChildren )
		{
			if ( child.SceneModel is null )
				continue;

			child.SceneModel.Transform = Transform.World;
			child.SceneModel.MergeBones( SceneModel );
		}
	}

	protected override void OnUpdate()
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
		return SceneModel?.GetAttachment( name, worldSpace );
	}
}
