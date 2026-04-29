using System.Collections.Generic;
using System.Linq;

namespace Sandbox.Events;

/// <summary>
/// Generate an ordering based on a set of first-most and last-most items, and
/// individual constraints between pairs of items. All first-most items will be
/// ordered before all last-most items, and any other items will be put in the
/// middle unless forced to be elsewhere by a constraint.
/// </summary>
internal class SortingHelper
{
	public record struct SortConstraint( int EarlierIndex, int LaterIndex )
	{
		public SortConstraint Complement => new ( LaterIndex, EarlierIndex );
	}

	private readonly int _itemCount;

	private readonly HashSet<SortConstraint> _initialConstraints = new HashSet<SortConstraint>();

	private readonly HashSet<int> _first = new HashSet<int>();
	private readonly HashSet<int> _last = new HashSet<int>();

	public SortingHelper( int itemCount )
	{
		_itemCount = itemCount;
	}

	public void AddConstraint( int earlierIndex, int laterIndex )
	{
		_initialConstraints.Add( new SortConstraint( earlierIndex, laterIndex ) );
	}

	public void AddFirst( int earlierIndex )
	{
		_first.Add( earlierIndex );
	}

	public void AddLast( int laterIndex )
	{
		_last.Add( laterIndex );
	}

	public bool Sort( List<int> result, out SortConstraint invalidConstraint )
	{
		var middle = new HashSet<int>();

		for ( var index = 0; index < _itemCount; ++index )
		{
			if ( !_first.Contains( index ) && !_last.Contains( index ) )
				middle.Add( index );
		}

		var allConstraints = new HashSet<SortConstraint>();
		var newConstraints = new Queue<SortConstraint>();
		var beforeDict = new Dictionary<int, HashSet<int>>();
		var afterDict = new Dictionary<int, HashSet<int>>();

		bool AddWorkingConstraint( int earlierIndex, int laterIndex, out SortConstraint constraint )
		{
			constraint = new SortConstraint( earlierIndex, laterIndex );

			if ( allConstraints.Contains( constraint.Complement ) )
				return false;

			if ( !allConstraints.Add( constraint ) )
				return true;

			newConstraints.Enqueue( constraint );

			if ( !beforeDict.TryGetValue( earlierIndex, out var before ) )
				beforeDict.Add( earlierIndex, before = new HashSet<int>() );

			if ( !afterDict.TryGetValue( laterIndex, out var after ) )
				afterDict.Add( laterIndex, after = new HashSet<int>() );

			before.Add( laterIndex );
			after.Add( earlierIndex );

			return true;
		}

		// Add initial constraints

		foreach ( var initialConstraint in _initialConstraints )
		{
			if ( !AddWorkingConstraint( initialConstraint.EarlierIndex, initialConstraint.LaterIndex, out invalidConstraint ) )
				return false;
		}

		// Everything in _first should be before everything in _last

		foreach ( var earlierIndex in _first )
		{
			foreach ( var laterIndex in _last )
			{
				if ( !AddWorkingConstraint( earlierIndex, laterIndex, out invalidConstraint ) )
					return false;
			}
		}

		// Keep propagating constraints until nothing changes

		while ( newConstraints.TryDequeue( out var nextConstraint ) )
		{
			// if a < b, and b < c, then a < c etc

			if ( beforeDict.TryGetValue( nextConstraint.LaterIndex, out var before ) )
			{
				foreach ( var laterIndex in before )
				{
					if ( !AddWorkingConstraint( nextConstraint.EarlierIndex, laterIndex, out invalidConstraint ) )
						return false;
				}
			}

			if ( afterDict.TryGetValue( nextConstraint.EarlierIndex, out var after ) )
			{
				foreach ( var earlierIndex in after )
				{
					if ( !AddWorkingConstraint( earlierIndex, nextConstraint.LaterIndex, out invalidConstraint ) )
					{
						return false;
					}
				}
			}
		}

		// Now if we have any items that aren't using GroupOrder.First, and haven't
		// determined that they are ordered before another item with GroupOrder.First,
		// we can safely order them after all GroupOrder.First items. And vice versa.

		foreach ( var middleIndex in middle )
		{
			var isBeforeAnyFirst = beforeDict.TryGetValue( middleIndex, out var before )
				&& before.Any( x => _first.Contains( x ) );

			var isAfterAnyLast = afterDict.TryGetValue( middleIndex, out var after )
				&& after.Any( x => _last.Contains( x ) );

			if ( !isBeforeAnyFirst )
			{
				foreach ( var earlierIndex in _first )
					AddWorkingConstraint( earlierIndex, middleIndex, out invalidConstraint );
			}

			if ( !isAfterAnyLast )
			{
				foreach ( var laterIndex in _last )
					AddWorkingConstraint( middleIndex, laterIndex, out invalidConstraint );
			}
		}

		// Now lets add items to the final ordering if all items that should be sorted
		// before them are already added to that ordering. We'll implement this by choosing
		// items that have an empty list / don't appear in afterDict, and update that
		// dictionary as we go.

		var earliestRemaining = new Queue<int>();

		// First, seed the queue with everything that's already not ordered after anything

		for ( var index = 0; index < _itemCount; ++index )
		{
			if ( !afterDict.ContainsKey( index ) )
			{
				earliestRemaining.Enqueue( index );
			}
		}

		result.Clear();

		while ( earliestRemaining.TryDequeue( out var nextIndex ) )
		{
			result.Add( nextIndex );

			foreach ( var laterIndex in beforeDict.TryGetValue( nextIndex, out var laterIndices )
				? laterIndices : Enumerable.Empty<int>() )
			{
				var beforeLater = afterDict[laterIndex];
				beforeLater.Remove( nextIndex );

				if ( beforeLater.Count == 0 )
					earliestRemaining.Enqueue( laterIndex );
			}
		}

		invalidConstraint = default;
		return result.Count == _itemCount;
	}
}
