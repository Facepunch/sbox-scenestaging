using Sandbox;
using Sandbox.Diagnostics;
using System.Linq;

public partial class Scene : GameObject
{
	public virtual void Load( GameResource resource )
	{
		Assert.NotNull( resource );

		ProcessDeletes();
		Clear();

		if ( resource is SceneFile sceneFile )
		{
			Source = sceneFile;

			using var sceneScope = Push();

			using var spawnScope = SceneUtility.DeferInitializationScope( "Load" );

			if ( sceneFile.GameObjects is not null )
			{
				foreach ( var json in sceneFile.GameObjects )
				{
					var go = CreateObject( false );
					go.Deserialize( json );
				}
			}
		}
	}

	public void LoadFromFile( string filename )
	{
		var file = ResourceLibrary.Get<SceneFile>( filename );
		if ( file is null )
		{
			Log.Warning( $"LoadFromFile: Couldn't find {filename}" );
			return;
		}

		Load( file );
	}

	public virtual GameResource Save()
	{
		var a = new SceneFile();
		a.GameObjects = Children.Select( x => x.Serialize() ).ToArray();
		return a;
	}
}
