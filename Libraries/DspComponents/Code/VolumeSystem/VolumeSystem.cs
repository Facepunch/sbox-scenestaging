using System.Collections.Generic;
using System.Linq;

namespace Sandbox.Volumes;

/// <summary>
/// A base GameObjectSystem for handling of VolumeComponents. The idea is that you're going to have
/// a custom VolumeComponent, and register your volumes in a VolumeGameObjectSystem derived GameObjectSystem.
/// This system's responsibility is primarily to store volumes and make them searchable.
/// </summary>
public abstract class VolumeSystem<T> : GameObjectSystem where T : VolumeComponent
{
	public VolumeSystem( Scene scene ) : base( scene )
	{

	}

	HashSet<T> volumes = new HashSet<T>();

	public void Add( T volume )
	{
		volumes.Add( volume );
	}

	public void Remove( T volume )
	{
		volumes.Remove( volume );
	}

	public T FindVolume( Vector3 position )
	{
		return FindAll( position ).FirstOrDefault();
	}

	public IEnumerable<T> FindAll( Vector3 position )
	{
		foreach ( var volume in volumes )
		{
			if ( !volume.SceneVolume.Test( volume.Transform.World, position ) )
				continue;

			yield return volume;
		}

	}
}
