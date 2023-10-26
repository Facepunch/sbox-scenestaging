using Sandbox;
using Sandbox.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;

public sealed partial class AnimatedModelComponent
{
	Dictionary<int, GameObject> boneToGameObject = new ();

	void BuildBoneHeirarchy( GameObject root, BoneCollection.Bone thisBone = null )
	{
		if ( Model is null ) 
			return;

		if ( thisBone is null )
		{
			ClearBoneProxies();

			if ( !CreateBoneObjects )
				return;
			
			foreach ( var b in Model.Bones.AllBones.Where( x => x.Parent is null ) )
			{
				BuildBoneHeirarchy( root, b );
			}

			return;
		}

		var boneGo = root.Children.FirstOrDefault( x => x.Name == thisBone.Name );

		if ( boneGo is null )
		{
			boneGo = Scene.CreateObject( true );
			boneGo.Parent = root;
		}

		boneToGameObject[thisBone.Index] = boneGo;

		boneGo.Flags |= GameObjectFlags.Bone;
		boneGo.Name = thisBone.Name;

		var proxy = new ModelBoneTransformProxy( this, thisBone, boneGo );
		boneGo.Transform.Proxy = proxy;

		foreach ( var b in thisBone.Children )
		{
			BuildBoneHeirarchy( boneGo, b );
		}
	}

	void ClearBoneProxies()
	{
		if ( boneToGameObject.Count == 0 )
			return;

		//Log.Info( $"Clearing {boneToGameObject.Count} Bones" );

		foreach ( var o in boneToGameObject )
		{
			if ( !o.Value.IsValid() )
				continue;

			var t = o.Value.Transform.World;

			o.Value.Transform.Proxy = null;
			o.Value.Flags &= ~GameObjectFlags.Bone;

			if ( _sceneObject is not null )
			{
				o.Value.Transform.World = t;
			}
		}

		boneToGameObject.Clear();
	}

	public Transform GetBoneTransform( in BoneCollection.Bone bone, in bool worldPosition )
	{
		if ( _sceneObject is null ) return global::Transform.Zero;

		return _sceneObject.GetBoneWorldTransform( bone.Index );
	}

	public void SetBoneTransform( in BoneCollection.Bone bone, in Transform tx, in bool worldPosition )
	{
		if ( _sceneObject is null ) return;

		_sceneObject.SetBoneWorldTransform( bone.Index, tx );
	}

	internal void SetPhysicsBone( int v, Transform transform, float lerp )
	{
		_sceneObject.SetBoneOverride( v, transform, lerp );
	}

	internal void ClearPhysicsBones()
	{
		if ( !_sceneObject.IsValid() ) return;

		_sceneObject.ClearBoneOverrides();
	}
}


public class ModelBoneTransformProxy : TransformProxy
{
	private AnimatedModelComponent model;
	private BoneCollection.Bone bone;
	private GameObject target;

	public ModelBoneTransformProxy( AnimatedModelComponent model, BoneCollection.Bone bone, GameObject target )
	{
		this.model = model;
		this.bone = bone;
		this.target = target;
	}

	public override Transform GetLocalTransform()
	{
		if ( !target.IsValid() ) return default;

		return target.Parent.Transform.World.ToLocal( target.Transform.World );
	}

	public override void SetLocalTransform( in Transform value )
	{
		if ( !target.IsValid() ) return;

		var world = target.Parent.Transform.World.ToWorld( value );
		SetWorldTransform( world );
	}

	public override Transform GetWorldTransform()
	{
		return model.GetBoneTransform( bone, true );
	}

	public override void SetWorldTransform( Transform value )
	{
		model.SetBoneTransform( bone, value, true );
	}
}
