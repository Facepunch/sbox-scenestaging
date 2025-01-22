using System;

namespace Sandbox.Animation;

#nullable enable

internal static class Extensions
{
	public static (float? Prev, float? Next) GetNeighborKeys<TValue>( this SortedList<float, TValue> list, float key )
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

	public static float Apply( this KeyframeEasing easing, float t ) => easing switch
	{
		KeyframeEasing.Linear => t,
		_ => 0f
	};
}
