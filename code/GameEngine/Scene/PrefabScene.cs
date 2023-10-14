using Sandbox;
using Sandbox.Diagnostics;
using Sandbox.Utility;
using System;
using System.Collections.Generic;
using System.Linq;

public sealed class PrefabScene : Scene
{
	public static PrefabScene Create()
	{
		return new PrefabScene();
	}
	
	/// <summary>
	/// Creates a scene from the PrefabFile. Doesn't actually contain anything, just used as a pointer to the PrefabFile.
	/// </summary>
	public static PrefabScene Create( PrefabFile file )
	{
		return new PrefabScene()
		{
			Source = file,
			Name = $"{file.ResourceName}"
		};
	}

	private PrefabScene() : base( true )
	{

	}

	public override void Load( GameResource resource )
	{
		Assert.NotNull( resource );

		Clear();

		if ( resource is not PrefabFile file )
		{
			Log.Warning( "Resource is not a PrefabFile" );
			return;
		}

		if ( file.RootObject is null )
		{
			Log.Warning( "PrefabFile RootObject is null" );
			return;
		}

		Source = file;
		using ( SceneUtility.DeferInitializationScope( "Load" ) )
		{
			Deserialize( file.RootObject );
		}

		Transform.Local = global::Transform.Zero.WithScale( Transform.LocalScale.x );
	}

	public override GameResource Save()
	{
		var a = new PrefabFile();
		a.RootObject = Serialize();
		return a;
	}
}
