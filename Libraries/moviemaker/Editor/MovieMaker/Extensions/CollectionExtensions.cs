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

	public static IEnumerable<T> Merge<T>( this IEnumerable<T> a, IEnumerable<T> b )
		where T : IComparable<T>
	{
		using var enumeratorA = a.GetEnumerator();
		using var enumeratorB = b.GetEnumerator();

		var hasItemA = enumeratorA.MoveNext();
		var hasItemB = enumeratorB.MoveNext();

		while ( hasItemA || hasItemB )
		{
			var lastItem = !hasItemB || hasItemA && enumeratorA.Current.CompareTo( enumeratorB.Current ) <= 0
				? enumeratorA.Current
				: enumeratorB.Current;

			yield return lastItem;

			while ( hasItemA && lastItem.CompareTo( enumeratorA.Current ) == 0 )
			{
				hasItemA = enumeratorA.MoveNext();
			}

			while ( hasItemB && lastItem.CompareTo( enumeratorB.Current ) == 0 )
			{
				hasItemB = enumeratorB.MoveNext();
			}
		}
	}
}
