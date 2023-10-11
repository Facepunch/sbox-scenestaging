using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

public partial class GameObject
{
	PrefabScene PrefabScene { get; set; }

	internal void SetPrefabSource( PrefabScene prefabScene )
	{
		PrefabScene = prefabScene;
	}

	public bool IsPrefabInstance
	{
		get
		{
			if ( IsPrefabInstanceRoot ) return true;
			if ( Parent is null ) return false;

			return Parent.IsPrefabInstance;
		}
	}

	public bool IsPrefabInstanceRoot
	{
		get
		{
			if ( PrefabScene is not null ) return true;
			return false;
		}
	}
}
