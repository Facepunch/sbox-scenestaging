using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

internal static class MovieExtensions
{
	/// <summary>
	/// Gets the <see cref="GameObject"/> that the given property is contained within.
	/// </summary>
	public static GameObject? GetTargetGameObject( this ITrackTarget property )
	{
		while ( property is ITrackProperty memberProperty )
		{
			property = memberProperty.Parent;
		}

		return property switch
		{
			ITrackReference<GameObject> goProperty => goProperty.Value,
			ITrackReference { Value: Component cmp } => cmp.GameObject,
			_ => null
		};
	}

	public static (MovieTime? Prev, MovieTime? Next) GetNeighborKeys<TValue>( this SortedList<MovieTime, TValue> list, MovieTime key )
	{
		if ( list.Count == 0 ) return (default, default);

		var keys = list.Keys;

		if ( keys[0] > key ) return (default, keys[0]);
		if ( keys[^1] <= key ) return (keys[^1], default);

		// Binary search

		var minIndex = 0;
		var maxIndex = keys.Count - 1;

		while ( maxIndex - minIndex > 1 )
		{
			var midIndex = (minIndex + maxIndex) >> 1;
			var midKey = keys[midIndex];

			if ( midKey > key )
			{
				maxIndex = midIndex;
			}
			else
			{
				minIndex = midIndex;
			}
		}

		return (keys[minIndex], keys[maxIndex]);
	}
}
