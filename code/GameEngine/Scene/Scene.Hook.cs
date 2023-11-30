using Sandbox;

public partial class Scene
{
	List<SceneHook> systems = new List<SceneHook>();

	/// <summary>
	/// Call dispose on all installed hooks
	/// </summary>
	void ShutdownHooks()
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
	void InitHooks()
	{
		ShutdownHooks();

		var found = TypeLibrary.GetTypes<SceneHook>()
			.Where( x => !x.IsAbstract )
			.ToArray();

		foreach( var f in found )
		{
			systems.Add( f.Create<SceneHook>( new object[] { this } ) );
		}
	}

	/// <summary>
	/// Signal a hook stage
	/// </summary>
	private void Signal( SceneHook.Stage stage )
	{
		GetHooks( stage ).Run();
	}

	Dictionary<SceneHook.Stage, TimedCallbackList> listeners = new Dictionary<SceneHook.Stage, TimedCallbackList>();

	/// <summary>
	/// Get the hook container for this stage
	/// </summary>
	TimedCallbackList GetHooks( SceneHook.Stage stage )
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
	public IDisposable AddHook( SceneHook.Stage stage, int order, Action action, string className, string description )
	{
		return GetHooks( stage ).Add( order, action, className, description );
	}
}
