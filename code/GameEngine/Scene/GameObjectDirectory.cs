using System;
using System.Collections.Generic;

/// <summary>
/// New GameObjects are registered with this class when they're created, and 
/// unregistered when they're removed. This gives us a single place to enforce
/// Id uniqueness in the scene, and allows for fast lookups by Id.
/// </summary>
public class GameObjectDirectory
{
	private Scene scene;

	Dictionary<Guid, GameObject> objectsById = new();

	public int Count => objectsById.Count;

	public GameObjectDirectory( Scene scene )
	{
		this.scene = scene;
	}

	internal void Add( GameObject go )
	{
		if ( objectsById.TryGetValue( go.Id, out var existing ) )
		{
			Log.Warning( $"{go}: Guid {go.Id} is already taken by {existing} - changing" );
			go.ForceChangeId( Guid.NewGuid() );
		}

		objectsById[go.Id] = go;
	}

	internal void Add( GameObject go, Guid previouslyKnownAs )
	{
		if ( go is Scene ) return;

		if ( objectsById.TryGetValue( previouslyKnownAs, out var existing ) && existing == go )
		{
			objectsById.Remove( previouslyKnownAs );
		}

		Add( go );
	}

	internal void Remove( GameObject go )
	{
		if ( go is Scene ) return;

		if ( !objectsById.TryGetValue( go.Id, out var existing ) )
		{
			Log.Warning( $"Tried to unregister unregistered id {go}, {go.Id}" );
			return;
		}

		if ( existing != go )
		{
			Log.Warning( $"Tried to unregister wrong game object {go}, {go.Id} (was {existing})" );
			return;

		}

		objectsById.Remove( go.Id );
	}

	/// <summary>
	/// Find a GameObject in the scene by Guid. This should be really really fast.
	/// </summary>
	public GameObject FindByGuid( Guid guid )
	{
		if ( objectsById.TryGetValue( guid, out var found ) )
			return found;

		return null;
	}
}
