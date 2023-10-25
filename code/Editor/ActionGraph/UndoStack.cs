using System;

namespace Editor.ActionGraph;

public class UndoStack
{
	private record struct Frame( string Name, string Value );

	private readonly List<Frame> _stack = new();
	private int _index;

	public bool CanUndo => _index > 1;
	public bool CanRedo => _index < _stack.Count;

	public string UndoName => CanUndo ? $"Undo {_stack[_index].Name}" : null;
	public string RedoName => CanRedo ? $"Redo {_stack[_index + 1].Name}" : null;

	public void Push( string name, string value )
	{
		if ( _index != _stack.Count )
		{
			_stack.RemoveRange( _index, _stack.Count - _index );
		}

		_stack.Add( new Frame( name, value ) );
		_index = _stack.Count;
	}

	public string Undo()
	{
		if ( !CanUndo )
		{
			throw new InvalidOperationException();
		}

		--_index;
		return _stack[_index].Value;
	}

	public string Redo()
	{
		if ( !CanRedo )
		{
			throw new InvalidOperationException();
		}

		++_index;
		return _stack[_index].Value;
	}

	public void Clear()
	{
		_stack.Clear();
		_index = 0;
	}
}
