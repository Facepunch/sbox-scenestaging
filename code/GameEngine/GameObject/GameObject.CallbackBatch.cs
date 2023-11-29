
using Sandbox;
using Sandbox.Diagnostics;

/// <summary>
/// Store callbacks until a set time, and then call them in grouped order
/// </summary>
internal class CallbackBatch : System.IDisposable
{
	static CallbackBatch Current { get; set; }
	static Stack<CallbackBatch> Pool = new ();

	class Group
	{
		List<Action> Actions = new List<Action>();

		public void Clear()
		{
			Actions.Clear();
		}

		public void Add( string name, Action action )
		{
			Actions.Add( action );
		}

		public void Execute()
		{
			foreach( var action in Actions )
			{
				action();
			}

			Actions.Clear();
		}
	}

	Dictionary<int, Group> Groups = new Dictionary<int, Group>();

	public static CallbackBatch StartGroup()
	{
		if ( Current is not null ) return null;

		if ( !Pool.TryPop( out var instance) )
		{
			instance = new CallbackBatch();
		}

		Current = instance;
		return Current;
	}

	public static void Add( string name, int order, Action action )
	{
		if ( Current is not null )
		{
			Current.AddToGroup( order, name, action );
			// add 
			return;
		}

		throw new System.Exception( $"CallbackBatch.Add called outside of a batch for '{name}'" );
	}

	void Execute()
	{
		int lastIndex = -1000;

		foreach( var group in Groups.OrderBy( x => x.Key ) )
		{
			Assert.True( group.Key > lastIndex );
			lastIndex = group.Key;
			group.Value.Execute();
		}
	}

	void AddToGroup( int order, string name, Action action )
	{
		var v = Groups.GetOrCreate( order );
		v.Add( name, action );
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

