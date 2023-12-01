using Sandbox;

public partial class Scene
{
	List<GameObjectSystem> systems = new List<GameObjectSystem>();

	/// <summary>
	/// Call dispose on all installed hooks
	/// </summary>
	void ShutdownSystems()
	{
		foreach( var sys in systems )
		{
			sys.Dispose();
		}

		systems.Clear();
	}

	/// <summary>
	/// Find all types of SceneHook, create an instance of each one and install it.
	/// </summary>
	void InitSystems()
	{
		ShutdownSystems();

		var found = TypeLibrary.GetTypes<GameObjectSystem>()
			.Where( x => !x.IsAbstract )
			.ToArray();

		foreach( var f in found )
		{
			systems.Add( f.Create<GameObjectSystem>( new object[] { this } ) );
		}
	}

	/// <summary>
	/// Signal a hook stage
	/// </summary>
	private void Signal( GameObjectSystem.Stage stage )
	{
		GetCallbacks( stage ).Run();
	}

	Dictionary<GameObjectSystem.Stage, TimedCallbackList> listeners = new Dictionary<GameObjectSystem.Stage, TimedCallbackList>();

	/// <summary>
	/// Get the hook container for this stage
	/// </summary>
	TimedCallbackList GetCallbacks( GameObjectSystem.Stage stage )
	{
		if ( listeners.TryGetValue( stage, out var list ) )
			return list;

		list = new TimedCallbackList();
		listeners[stage] = list;
		return list;
	}

	/// <summary>
	/// Call this method on this stage. This returns a disposable that will remove the hook when disposed.
	/// </summary>
	public IDisposable AddHook( GameObjectSystem.Stage stage, int order, Action action, string className, string description )
	{
		return GetCallbacks( stage ).Add( order, action, className, description );
	}
}
