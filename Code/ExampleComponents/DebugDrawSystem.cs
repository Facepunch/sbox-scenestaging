public partial class DebugDrawSystem : GameObjectSystem<DebugDrawSystem>
{
	bool inFixedUpdate;

	class Entry : IDebugShape, IDisposable
	{
		public bool CreatedDuringFixed;
		public bool SingleFrame = true;
		public float life;
		public SceneObject sceneObject;

		public Entry( float duration )
		{
			if ( duration > 0 )
			{
				life = duration;
				SingleFrame = false;
			}
		}

		public void Dispose()
		{
			sceneObject?.Delete();
			sceneObject = default;
		}

		IDebugShape IDebugShape.WithColor( Color color )
		{
			if ( color == default ) color = Color.White;

			if ( sceneObject is not null )
			{
				sceneObject.Attributes.Set( "g_tint", color );
				sceneObject.ColorTint = color;
			}

			return this;
		}

		IDebugShape IDebugShape.WithTime( float time )
		{
			if ( time <= 0 ) return this;

			life = time;
			SingleFrame = false;
			return this;
		}
	}

	List<Entry> entries = new List<Entry>();

	public interface IDebugShape
	{
		public IDebugShape WithColor( Color color )
		{
			return this;
		}

		public IDebugShape WithTime( float time );
	}


	public DebugDrawSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.StartUpdate, -10000, StartUpdate, "BuildDebugOverlays" );
		Listen( Stage.StartFixedUpdate, -10000, StartFixedUpdate, "BuildDebugOverlays" );
		Listen( Stage.FinishFixedUpdate, 10000, EndFixedUpdate, "BuildDebugOverlays" );

		LineMaterial = Material.Load( "materials/gizmo/line.vmat" );
	}

	IDebugShape Add( Entry entry )
	{
		entries.Add( entry );
		entry.CreatedDuringFixed = inFixedUpdate;
		return entry;
	}

	void RemoveExpired()
	{
		for ( int i = 0; i < entries.Count; i++ )
		{
			if ( entries[i].SingleFrame ) continue;
			entries[i].life -= Time.Delta;

			if ( entries[i].life < 0 )
			{
				entries[i].Dispose();
				entries.RemoveAt( i ); // move to end and remove?
				i--;
			}
		}
	}

	void RemoveSingleFrame( bool createdDuringFixed )
	{
		for ( int i = 0; i < entries.Count; i++ )
		{
			if ( !entries[i].SingleFrame ) continue;
			if ( entries[i].CreatedDuringFixed != createdDuringFixed ) continue;

			entries[i].Dispose();
			entries.RemoveAt( i );
			i--;
		}

		Scene.SceneWorld.DeletePendingObjects();
	}

	void StartUpdate()
	{
		RemoveExpired();
		RemoveSingleFrame( false );
	}

	void StartFixedUpdate()
	{
		RemoveSingleFrame( true );
		inFixedUpdate = true;
	}

	void EndFixedUpdate()
	{
		inFixedUpdate = false;
	}
}
