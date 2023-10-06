using Sandbox;
using Sandbox.Diagnostics;
using Sandbox.Utility;
using System;
using System.Collections.Generic;
using System.Linq;

public sealed class PrefabScene : Scene
{
	public PrefabFile Source { get; set; }

	public void Load( PrefabFile resource )
	{
		Source = resource;

		using var spawnScope = SceneUtility.DeferInitializationScope( "Load" );

		if ( resource.RootObject is not null )
		{
			Deserialize( resource.RootObject );
		}
	}
}
