using Editor.SoundEditor;
using System;
using System.Reflection;

namespace Sandbox.Helpers;

/// <summary>
/// A system that aims to wrap the main reusable functionality of an undo system
/// </summary>
public partial class UndoSystem
{
	public class Entry
	{
		public string Name { get; set; }
		public Action Undo { get; set; }
		public Action Redo { get; set; }
		public Object Image { get; set; }
		public DateTime Timestamp { get; set; }
		public bool Locked { get; set; }
	}

	/// <summary>
	/// Called when an undo is run
	/// </summary>
	public Action<Entry> OnUndo;

	/// <summary>
	/// Called when a redo is run
	/// </summary>
	public Action<Entry> OnRedo;

	/// <summary>
	/// Back stack
	/// </summary>
	internal Stack<Entry> Back { get; set; } = new();

	/// <summary>
	/// Forward stack, gets cleared when a new undo is added
	/// </summary>
	internal Stack<Entry> Forward { get; set; } = new();

	/// <summary>
	/// Instigate an undo. Return true if we found a successful undo
	/// </summary>
	public bool Undo()
	{
		if ( !Back.TryPop( out var entry ) )
		{
			next = initial;
			return false;
		}

		//
		// If we don't have a redo, try to take a snapshot
		//
		if ( entry.Redo == null )
		{
			entry.Redo = snapshotFunction?.Invoke();
		}

		next = entry.Undo;
		try
		{
			entry.Undo?.Invoke();
		}
		catch ( System.Exception e )
		{
			Log.Warning( e, $"Error when undoing '{entry.Name}': {e.Message}" );
		}

		if ( entry.Locked )
		{
			Back.Push( entry );
			return false;
		}

		Forward.Push( entry );
		OnUndo?.Invoke( entry );

		return true;
	}

	/// <summary>
	/// Instigate a redo, returns true if we found a successful undo
	/// </summary>
	public bool Redo()
	{
		if ( !Forward.TryPop( out var entry ) )
			return false;

		Back.Push( entry );
		entry.Redo?.Invoke();
		OnRedo?.Invoke( entry );

		return true;
	}

	/// <summary>
	/// Insert a new undo entry
	/// </summary>
	public Entry Insert( string title, Action undo, Action redo = null )
	{
		var e = new Entry
		{
			Name = title,
			Undo = undo,
			Redo = redo,
			Timestamp = DateTime.Now
		};

		Back.Push( e );

		Forward.Clear();

		return e;
	}

	Func<Action> snapshotFunction;

	/// <summary>
	/// Provide a function that returns an action to call on undo/redo.
	/// This generally is a function that saves and restores the entire state
	/// of a project.
	/// </summary>
	public void SetSnapshotFunction( Func<Action> snapshot )
	{
		snapshotFunction = snapshot;
	}

	/// <code>
	///  func getsnapshot()
	///  {
	///		var state = currentstate();
	///
	///		return () => restorestate( state );
	///  }
	///
	///  startup()
	///  {
	///     -- give a function that creates undo functions
	///     UndoSystem.SetSnapshotter( getsnapshot )
	///
	///     -- store current snapshot in `next`
	///     UndoSystem.Initialize();
	///  }
	///
	///  mainloop()
	///  {
	///     deleteobject();
	///
	///     -- store 'next' snapshot as "object deleted" undo
	///     -- take a new snapshot and store it in next
	///     UndoSystem.Snapshot( "object deleted" );
	///  }
	/// </code>
	Action next;
	Action initial;

	/// <summary>
	/// Should be called after you make a change to your project. The snapshot system
	/// is good for self contained projects that can be serialized and deserialized quickly.
	/// </summary>
	public void Snapshot( string changeTitle )
	{
		if ( snapshotFunction == null )
			return;

		Insert( changeTitle, next );
		next = snapshotFunction?.Invoke();
	}

	/// <summary>
	/// Clear the history and take an initial snapshot.
	/// You should call this right after a load, or a new project.
	/// </summary>
	public void Initialize()
	{
		Back.Clear();
		Forward.Clear();

		next = snapshotFunction?.Invoke();
		initial = next;
	}
}
