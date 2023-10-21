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
		if ( Model is null ) return;

		if ( thisBone is null )
		{
			boneToGameObject.Clear();

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
		boneGo.Transform.Local = thisBone.LocalTransform;

		foreach ( var b in thisBone.Children )
		{
			BuildBoneHeirarchy( boneGo, b );
		}
	}

	void UpdateBoneTransforms()
	{
		if ( _sceneObject is null )
			return;

		using var pscope = Sandbox.Utility.Superluminal.Scope( "UpdateBoneTransforms", Color.Cyan );

		foreach ( var c in boneToGameObject )
		{
			var tx = _sceneObject.GetBoneWorldTransform( c.Key );
			c.Value.Transform.World = tx;
		}
	}
}
