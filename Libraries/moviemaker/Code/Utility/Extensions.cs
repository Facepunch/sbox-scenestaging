using System;

namespace Sandbox.MovieMaker;

#nullable enable

public static class Extensions
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
			case ArraySegment<T> segment:
				return segment.Slice( offset, count );
		}

		// Slow copy

		return list.Skip( offset ).Take( count ).ToArray();
	}
}
