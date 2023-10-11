using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

public partial class GameObject
{
	string PrefabSource { get; set; }
	Dictionary<Guid, Guid> PrefabLut { get; set; }

	internal void SetPrefabSource( string prefabSource )
	{
		PrefabSource = prefabSource;
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
			if ( PrefabSource is not null ) return true;
			return false;
		}
	}
}
