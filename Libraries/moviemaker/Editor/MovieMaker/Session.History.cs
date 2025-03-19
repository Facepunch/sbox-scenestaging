namespace Editor.MovieMaker;

#nullable enable

public interface ISessionAction
{
	string Title { get; }

	bool Apply( Session session );
	bool Revert( Session session );

	ISessionAction? Merge( ISessionAction next ) => null;
}

public interface ISessionAction<T> : ISessionAction
	where T : EditMode
{
	bool Apply( T editMode );
	bool Revert( T editMode );

	bool ISessionAction.Apply( Session session )
	{
		session.SetEditMode<T>();
		return Apply( (T)session.EditMode! );
	}

	bool ISessionAction.Revert( Session session )
	{
		session.SetEditMode<T>();
		return Revert( (T)session.EditMode! );
	}
}

public sealed class NullAction : ISessionAction
{
	public string Title => "Null";

	public bool Apply( Session session ) => false;
	public bool Revert( Session session ) => false;
}

public sealed class SessionHistory
{
	private readonly Session _session;

	private readonly Stack<ISessionAction> _undoStack = new();
	private readonly Stack<ISessionAction> _redoStack = new();

	public SessionHistory( Session session )
	{
		_session = session;
	}

	public void Push( ISessionAction action )
	{
		if ( !action.Apply( _session ) ) return;

		if ( _undoStack.TryPeek( out var last ) && last.Merge( action ) is { } merged )
		{
			_undoStack.Pop();

			if ( merged is NullAction ) return;

			_undoStack.Push( merged );
			return;
		}

		_redoStack.Clear();
		_undoStack.Push( action );
	}

	public bool Undo()
	{
		if ( !_undoStack.TryPop( out var action ) ) return false;

		_redoStack.Push( action );

		return action.Revert( _session );
	}

	public bool Redo()
	{
		if ( !_redoStack.TryPop( out var action ) ) return false;

		_undoStack.Push( action );

		return action.Apply( _session );
	}
}

partial class Session
{
	public SessionHistory History { get; }
}
