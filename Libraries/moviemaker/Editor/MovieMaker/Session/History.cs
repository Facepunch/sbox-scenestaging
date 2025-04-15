using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

public sealed class SessionHistory
{
	private readonly Session _session;

	private sealed class Item
	{
		public string Title { get; }
		public SessionSnapshot Before { get; }
		public SessionSnapshot After { get; set; }

		public bool IsEmpty => Before.Equals( After );

		public Item( string title, SessionSnapshot before )
		{
			Title = title;
			Before = before;
			After = before;
		}

		public bool Apply( Session session )
		{
			return session.Restore( After );
		}

		public bool Revert( Session session )
		{
			return session.Restore( Before );
		}
	}

	public interface IScope : IDisposable
	{
		void PostChange();
		void IDisposable.Dispose() => PostChange();
	}

	private sealed record Scope( Session Session, Item Item ) : IScope
	{
		public void PostChange()
		{
			Item.After = Session.Snapshot();
		}
	}

	private readonly Stack<Item> _undoStack = new();
	private readonly Stack<Item> _redoStack = new();

	public bool CanUndo => _undoStack.Count > 0;
	public bool CanRedo => _redoStack.Count > 0;

	public SessionHistory( Session session )
	{
		_session = session;
	}

	public IScope Push( string title )
	{
		var before = _undoStack.TryPeek( out var last )
			? last.After
			: _session.Snapshot();

		var item = new Item( title, before );

		_redoStack.Clear();
		_undoStack.Push( item );

		return new Scope( _session, item );
	}

	public bool Undo()
	{
		if ( !_undoStack.TryPop( out var item ) ) return false;

		_redoStack.Push( item );

		return item.Revert( _session );
	}

	public bool Redo()
	{
		if ( !_redoStack.TryPop( out var item ) ) return false;

		_undoStack.Push( item );

		return item.Apply( _session );
	}
}

internal sealed record SessionProperties(
	MovieTime TimeOffset,
	float PixelsPerSecond,
	int FrameRate );

internal sealed record EditModeSnapshot(
	Type Type, EditMode.ISnapshot? Data );
internal sealed record SessionSnapshot(
	MovieProject.Model Project,
	EditModeSnapshot? EditMode,
	SessionProperties Properties );

partial class Session
{
	public SessionHistory History { get; }

	internal SessionSnapshot Snapshot() => new (
		Project.Snapshot(), 
		EditMode?.Snapshot(),
		new SessionProperties( TimeOffset, PixelsPerSecond, FrameRate ) );

	internal bool Restore( SessionSnapshot snapshot )
	{
		Project.Restore( snapshot.Project );

		if ( snapshot.EditMode is { } editMode && SetEditMode( editMode.Type ) && editMode.Data is { } data )
		{
			EditMode?.Restore( data );
		}

		SetView( snapshot.Properties.TimeOffset, snapshot.Properties.PixelsPerSecond );

		FrameRate = snapshot.Properties.FrameRate;

		TrackList.Update();

		return true;
	}
}

partial class EditMode
{
	protected internal interface ISnapshot;

	internal EditModeSnapshot Snapshot() => new( GetType(), OnSnapshot() );

	protected virtual ISnapshot? OnSnapshot() => null;

	internal void Restore( ISnapshot snapshot ) => OnRestore( snapshot );
	protected virtual void OnRestore( ISnapshot snapshot ) { }
}
