using Sandbox;
using Sandbox.Diagnostics;
using Sandbox.Utility;
using System;
using System.Collections.Generic;
using System.Linq;

public sealed class PrefabScene : Scene
{
	public override void Load( GameResource resource )
	{
		Assert.NotNull( resource );

		Clear();

		if ( resource is PrefabFile sceneFile )
		{
			Source = sceneFile;
			using var spawnScope = SceneUtility.DeferInitializationScope( "Load" );

			if ( sceneFile.RootObject is not null )
			{
				Deserialize( sceneFile.RootObject );
			}
		}
	}

	public override GameResource Save()
	{
		var a = new PrefabFile();
		a.RootObject = Serialize();
		return a;
	}
}
