using Sandbox.Diagnostics;
using Sandbox.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json.Nodes;

public static class SceneUtility
{
	/// <summary>
	/// Find all "Id" guids, and replace them with new Guids.
	/// This is used to make GameObject serializations unique, so when
	/// you duplicate stuff, it copies over uniquely and keeps associations.
	/// </summary>
	public static void MakeGameObjectsUnique( JsonObject json )
	{
		Dictionary<Guid, Guid> translate = new();

		//
		// Find all guids with "Id" as their name. Add them to translate 
		// with a new target value.
		//
		Sandbox.Json.WalkJsonTree( json, ( k, v ) =>
		{
			if ( k != "Id" ) return v;

			if ( v.TryGetValue<Guid>( out var guid ) )
			{
				translate[guid] = Guid.NewGuid();
			}

			return v;
		} );

		//
		// Find every guid and translate them, but only if they're in our
		// guid dictionary.
		//
		Sandbox.Json.WalkJsonTree( json, ( k, v ) =>
		{
			if ( !v.TryGetValue<Guid>( out var guid ) ) return v;
			if ( !translate.TryGetValue( guid, out var updatedGuid ) ) return v;

			return updatedGuid;
		} );
	}


	/// <summary>
	/// Create a unique copy of the passed in GameObject
	/// </summary>
	public static GameObject Instantiate( GameObject template, Transform transform )
	{
		Assert.NotNull( GameManager.ActiveScene, "No Active Scene" );

		using var batchGroup = CallbackBatch.StartGroup();

		JsonObject json = null;

		if ( template is PrefabScene prefabScene && prefabScene.Source is PrefabFile prefabFile )
		{
			json = prefabFile.RootObject;
		}
		else
		{
			json = template.Serialize();
		}

		MakeGameObjectsUnique( json );

		var go = new GameObject();
		go.Deserialize( json );
		go.Transform.Local = transform.WithScale( transform.Scale * go.Transform.LocalScale.x );

		if ( template is PrefabScene prefabScene1 )
		{
			go.SetPrefabSource( prefabScene1.Source.ResourcePath );
		}

		go.Parent = GameManager.ActiveScene;

		return go;
	}

	/// <summary>
	/// Create a unique copy of the passed in GameObject
	/// </summary>
	public static GameObject Instantiate( GameObject template ) => Instantiate( template, Transform.Zero );

	/// <summary>
	/// Create a unique copy of the passed in GameObject
	/// </summary>
	public static GameObject Instantiate( GameObject template, Vector3 position, Rotation rotation ) 
		=> Instantiate( template, new Transform( position, rotation, 1.0f ) );

	/// <summary>
	/// Create a unique copy of the passed in GameObject
	/// </summary>
	public static GameObject Instantiate( GameObject template, Vector3 position )
		=> Instantiate( template, new Transform( position, Rotation.Identity, 1.0f ) );
}
