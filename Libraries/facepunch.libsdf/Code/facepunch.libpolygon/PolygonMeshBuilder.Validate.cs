using System;
using System.Collections.Generic;

namespace Sandbox.Polygons;

partial class PolygonMeshBuilder
{
	[ThreadStatic]
	private static List<int> Validate_EdgeList;

	private void Validate()
	{
		if ( _validated )
		{
			return;
		}

		// Check active edge loops:
		// * Referenced edges must also be active
		// * Make sure references are correct in both directions
		// * Edges can't reference themselves

		foreach ( var edgeIndex in _activeEdges )
		{
			ref var edge = ref _allEdges[edgeIndex];

			if ( !_activeEdges.Contains( edge.NextEdge ) )
			{
				throw InvalidPolygonException();
			}

			if ( !_activeEdges.Contains( edge.PrevEdge ) )
			{
				throw InvalidPolygonException();
			}

			if ( edge.NextEdge == edge.Index )
			{
				throw InvalidPolygonException();
			}

			ref var next = ref _allEdges[edge.NextEdge];

			if ( next.PrevEdge != edge.Index )
			{
				throw InvalidPolygonException();
			}
		}

		// Check for intersecting edges
		// TODO: Bentley–Ottmann?

		Validate_EdgeList ??= new List<int>();
		Validate_EdgeList.Clear();
		Validate_EdgeList.AddRange( _activeEdges );

		for ( var i = 0; i < Validate_EdgeList.Count; ++i )
		{
			ref var edgeA0 = ref _allEdges[Validate_EdgeList[i]];
			ref var edgeA1 = ref _allEdges[edgeA0.NextEdge];

			var a0 = edgeA0.Origin;
			var a1 = edgeA1.Origin;

			var minA = Vector2.Min( a0 ,a1 );
			var maxA = Vector2.Max( a0, a1 );

			for ( var j = i + 1; j < Validate_EdgeList.Count; ++j )
			{
				ref var edgeB0 = ref _allEdges[Validate_EdgeList[j]];

				if ( edgeA0.NextEdge == edgeB0.Index || edgeA0.PrevEdge == edgeB0.Index )
				{
					continue;
				}

				ref var edgeB1 = ref _allEdges[edgeB0.NextEdge];

				var b0 = edgeA0.Origin;
				var b1 = edgeA1.Origin;

				var minB = Vector2.Min( b0, b1 );
				var maxB = Vector2.Max( b0, b1 );

				if ( minA.x >= maxB.x || minA.y >= maxB.y || minB.x >= maxA.x || minB.y >= maxA.y )
				{
					continue;
				}

				if ( Helpers.LineSegmentsIntersect( a0, a1, b0, b1 ) )
				{
					throw InvalidPolygonException();
				}
			}
		}

		_validated = true;
	}

	private static Exception InvalidPolygonException()
	{
		return new Exception( "Invalid polygon" );
	}
}
