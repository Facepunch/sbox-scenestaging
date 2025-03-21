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
