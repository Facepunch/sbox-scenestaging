
using Sandbox;

/// <summary>
/// We want to execute callbacks in a predictable order. This happens
/// naturally when spawning one GameObject, but when spawning a scene, or a 
/// prefab, we want to hold the calls to things like OnEnable and call them all
/// after OnStart or whatever has been called on all the objects in the batch.
/// </summary>
internal class CallbackBatch : System.IDisposable
{
	static CallbackBatch Current { get; set; }
	static Stack<CallbackBatch> Pool = new();

	record struct ActionTarget( Action Action, object Target, string name );

	class Group
	{
		List<ActionTarget> Actions = new List<ActionTarget>();

		public void Clear()
		{
			Actions.Clear();
		}

		public void Add( ActionTarget action )
		{
			Actions.Add( action );
		}

		public void Execute()
		{
			foreach ( var action in Actions )
			{
				try
				{
					action.Action();
				}
				catch ( System.Exception e )
				{
					Log.Error( e, $"{action.name} on {action.Target} failed: {e.Message}" );
				}
			}

			Actions.Clear();
		}
	}

	Dictionary<CommonCallback, Group> Groups = new();

	public static CallbackBatch StartGroup()
	{
		if ( Current is not null ) return null;

		if ( !Pool.TryPop( out var instance ) )
		{
			instance = new CallbackBatch();
		}

		Current = instance;
		return Current;
	}

	public static void Add( CommonCallback order, Action action, object target, string name )
	{
		if ( Current is not null )
		{
			var group = Current.Groups.GetOrCreate( order );
			group.Add( new ActionTarget( action, target, name ) );
			return;
		}

		throw new System.Exception( $"CallbackBatch.Add called outside of a batch for '{order}'" );
	}

	void Execute()
	{
		foreach ( var group in Groups.OrderBy( x => x.Key ) )
		{
			group.Value.Execute();
		}
	}

	public void Dispose()
	{
		if ( Current == this )
		{
			Current = null;
		}

		Execute();

		if ( Pool.Count < 2 )
		{
			Pool.Push( this );
		}
	}
}

/// <summary>
/// A list of component methods that are deferred and batched into groups, and exected in group order.
/// This is used to ensure that components are initialized in a predictable order.
/// The order of this enum is critical.
/// </summary>
internal enum CommonCallback
{
	Unknown,

	/// <summary>
	/// The component is deserializing.
	/// </summary>
	Deserialize,

	/// <summary>
	/// The component has been deserialized, or edited in the editor
	/// </summary>
	Validate,

	/// <summary>
	/// An opportunity for the component to load any data they need to load
	/// </summary>
	Loading,

	/// <summary>
	/// The component is awake. Called only once, on first enable.
	/// </summary>
	Awake,

	/// <summary>
	/// Component has been enabled
	/// </summary>
	Enable,

	/// <summary>
	/// The component has become dirty, usually due to a property changing
	/// </summary>
	Dirty,

	/// <summary>
	/// Component has been disabled
	/// </summary>
	Disable,

	/// <summary>
	/// Component has been destroyed
	/// </summary>
	Destroy,

	/// <summary>
	/// GameObject actually deleted
	/// </summary>
	Term
}
