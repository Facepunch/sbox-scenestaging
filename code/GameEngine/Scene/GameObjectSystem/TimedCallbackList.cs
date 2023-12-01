
using Sandbox.Utility;
namespace Sandbox;

/// <summary>
/// Keeps a list of callbacks
/// The intention of this is that in the future we'll have a nice window that will
/// show the relative performance of each callback, and allow you to disable them to debug.
/// </summary>
class TimedCallbackList
{
	private List<CallbackEntry> entries = new ();

	public TimedCallbackList()
	{
	}

	private void Add( CallbackEntry entry )
	{
		// todo - add the entry in the right place relative to .Order
		entries.Add( entry );
	}

	private void Remove( CallbackEntry entry )
	{
		entries.Remove( entry );
	}

	internal IDisposable Add( int order, Action action, string className, string description )
	{
		var entry = new CallbackEntry( order, action, className, description );

		Add( entry );

		return DisposeAction.Create( () => Remove( entry ) );
	}

	public void Run()
	{
		for( int i=0; i<entries.Count; i++ )
		{
			entries[i].Run();
		}
	}

	public class CallbackEntry
	{
		private Action action;

		public int Order { get; private set; }
		public string ClassName { get; private set; }
		public string Description { get; private set; }

		public CallbackEntry( int order, Action action, string className, string description )
		{
			this.action = action;

			Order = order;
			ClassName = className;
			Description = description;
		}

		public void Run()
		{
			try
			{
				using ( Sandbox.Utility.Superluminal.Scope( Description, Color.White ) )
				{
					action();
				}
			}
			catch ( System.Exception e )
			{
				Log.Error( e, $"{ClassName}.{Description}: {e.Message}" );
			}
		}
	}
}
