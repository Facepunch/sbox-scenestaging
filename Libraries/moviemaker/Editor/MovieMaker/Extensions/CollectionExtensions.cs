using System.Collections;
using Sandbox.MovieMaker;
using System.Collections.Immutable;
using System.Linq;

namespace Editor.MovieMaker;

#nullable enable
public static class CollectionExtensions
{
	public static IReadOnlyList<T> Slice<T>( this IReadOnlyList<T> list, int offset, int count )
	{
		if ( offset < 0 )
		{
			throw new ArgumentException( "Offset must be >= 0.", nameof( offset ) );
		}

		if ( list.Count < offset + count )
		{
			throw new ArgumentException( "Slice exceeds list element count.", nameof( count ) );
		}

		// Fast paths

		if ( count == 0 ) return Array.Empty<T>();
		if ( offset == 0 && count == list.Count ) return list;

		switch ( list )
		{
			case T[] array:
				return new ArraySegment<T>( array, offset, count );
			case ImmutableArray<T> immutableArray:
				return immutableArray.Slice( offset, count );
			case ArraySegment<T> segment:
				return segment.Slice( offset, count );
		}

		// Slow copy

		return list.Skip( offset ).Take( count ).ToArray();
	}

	/// <summary>
	/// Given two ascending lists of time ranges, union any overlapping pairs between the two lists and return them.
	/// </summary>
	public static IEnumerable<MovieTimeRange> Union( this IEnumerable<MovieTimeRange> first, IEnumerable<MovieTimeRange> second )
	{
		using var enumeratorA = first.GetEnumerator();
		using var enumeratorB = second.GetEnumerator();

		var hasItemA = enumeratorA.MoveNext();
		var hasItemB = enumeratorB.MoveNext();

		while ( hasItemA || hasItemB )
		{
			var next = !hasItemB || hasItemA && enumeratorA.Current.Start <= enumeratorB.Current.Start
				? enumeratorA.Current
				: enumeratorB.Current;

			while ( true )
			{
				if ( hasItemA && next.Intersect( enumeratorA.Current ) is not null )
				{
					next = next.Union( enumeratorA.Current );
					hasItemA = enumeratorA.MoveNext();
					continue;
				}

				if ( hasItemB && next.Intersect( enumeratorB.Current ) is not null )
				{
					next = next.Union( enumeratorB.Current );
					hasItemB = enumeratorB.MoveNext();
					continue;
				}

				break;
			}

			yield return next;
		}
	}
}

public sealed class SynchronizedList<TSrc, TItem> : IReadOnlyList<TItem>
	where TSrc : notnull
{
	private readonly Dictionary<TSrc, TItem> _items = new();
	private readonly Dictionary<TSrc, int> _indices = new();

	private readonly List<TSrc> _orderedSources = new();

	private readonly Func<TSrc, TItem> _addFunc;
	private readonly Action<TSrc, TItem>? _removeAction;
	private readonly Func<TSrc, TItem, bool>? _updateAction;

	public SynchronizedList( Func<TSrc, TItem> addFunc, Action<TSrc, TItem>? removeAction, Func<TSrc, TItem, bool>? updateAction = null )
	{
		_addFunc = addFunc;
		_removeAction = removeAction;
		_updateAction = updateAction;
	}

	public void Clear() => Update( [] );

	public bool Update( IEnumerable<TSrc> source )
	{
		_indices.Clear();

		foreach ( var src in source )
		{
			_indices.Add( src, _indices.Count );
		}

		var changed = false;

		// Remove items

		for ( var i = _orderedSources.Count - 1; i >= 0; --i )
		{
			var src = _orderedSources[i];

			if ( _indices.ContainsKey( src ) ) continue;

			_orderedSources.RemoveAt( i );

			if ( !_items.Remove( src, out var item ) ) continue;

			_removeAction?.Invoke( src, item );

			changed = true;
		}

		// Add items

		foreach ( var src in _indices.Keys )
		{
			if ( _items.ContainsKey( src ) ) continue;

			var item = _addFunc( src );

			_orderedSources.Add( src );
			_items.Add( src, item );

			changed = true;
		}

		// Sort and update items

		_orderedSources.Sort( ( a, b ) => _indices[a] - _indices[b] );

		if ( _updateAction is { } updateAction )
		{
			foreach ( var src in _orderedSources )
			{
				changed |= updateAction( src, _items[src] );
			}
		}

		return changed;
	}

	public IEnumerator<TItem> GetEnumerator() => _orderedSources.Select(x => _items[x] ).GetEnumerator();
	IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();

	public int Count => _items.Count;

	public TItem this[ int index ] => _items[_orderedSources[index]];
}
