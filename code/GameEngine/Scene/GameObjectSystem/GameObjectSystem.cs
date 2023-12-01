namespace Sandbox;

/// <summary>
/// Allows creation of a system that always exists in every scene, 
/// is hooked into the scene's lifecycle, 
/// and is disposed when the scene is disposed.
/// </summary>
public abstract class GameObjectSystem : IDisposable
{
	public Scene Scene { get; private set; }

	List<IDisposable> disposables = new List<IDisposable>();

	public GameObjectSystem( Scene scene )
	{
		Scene = scene;
	}

	public virtual void Dispose()
	{
		foreach( var d in disposables )
		{
			d.Dispose();
		}

		Scene = null;
	}

	/// <summary>
	/// Listen to a frame stage. Order is used to determine the order in which listeners are called, the default action always happens at 0, so if you
	/// want it to happen before you should go to -1, if you want it to happen after go to 1 etc.
	/// </summary>
	protected void Listen( Stage stage, int order, Action function, string debugName )
	{
		var d = Scene.AddHook( stage, order, function, GetType().Name, debugName );
		disposables.Add( d );
	}

	/// <summary>
	/// A list of stages in the scene tick in which we can hook
	/// </summary>
	public enum Stage
	{
		// Bones are worked out
		UpdateBones,

		// Physics step
		PhysicsStep
	}

}
