public partial class DebugDrawSystem
{
	List<Entry> entries = new();

	class Entry : IDisposable
	{
		public bool CreatedDuringFixed;
		public bool SingleFrame = true;
		public float life;
		public SceneObject sceneObject;

		public Entry( float duration, bool fixedUpdate, SceneObject so )
		{
			CreatedDuringFixed = fixedUpdate;
			sceneObject = so;

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
	}

	/// <summary>
	/// Add an entry
	/// </summary>
	void Add( float duration, SceneObject so )
	{
		var entry = new Entry( duration, inFixedUpdate, so );
		entries.Add( entry );
	}
}
