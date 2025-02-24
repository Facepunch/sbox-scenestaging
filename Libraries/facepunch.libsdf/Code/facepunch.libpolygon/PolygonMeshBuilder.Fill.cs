using System;
using System.Collections.Generic;
using System.Linq;

namespace Sandbox.Polygons;

partial class PolygonMeshBuilder
{
	/// <summary>
	/// Triangulate any remaining active edges so that the generated mesh is closed.
	/// </summary>
	public PolygonMeshBuilder Fill()
	{
		Validate();

		Fill_UpdateExistingVertices();
		Fill_SplitIntoMonotonicPolygons();
		Fill_Triangulate();

		PostBevel();

		return this;
	}

	private enum SweepEvent
	{
		Start,
		End,
		Split,
		Merge,
		Upper,
		Lower
	}

	private static SweepEvent CategorizeEvent( in Edge prev, in Edge curr, in Edge next )
	{
		var prevLeft = Compare( prev.Origin, curr.Origin ) < 0;
		var nextLeft = Compare( next.Origin, curr.Origin ) < 0;

		var nextBelow = curr.Tangent.y < -prev.Tangent.y;

		switch (prevLeft, nextLeft, nextBelow)
		{
			case (false, false, false ):
				return SweepEvent.Start;

			case (true, true, true ):
				return SweepEvent.End;

			case (false, false, true ):
				return SweepEvent.Split;

			case (true, true, false ):
				return SweepEvent.Merge;

			case (true, false, _ ):
				return SweepEvent.Upper;

			case (false, true, _ ):
				return SweepEvent.Lower;
		}
	}

	[ThreadStatic]
	private static List<int> Fill_SortedEdges;

	[ThreadStatic]
	private static Dictionary<int, (int Index, bool WasMerge)> Fill_Helpers;

	[ThreadStatic]
	private static List<SweepEdge> Fill_SweepEdges;

	private readonly struct SweepEdge
	{
		public int Index { get; }

		public Vector2 Origin { get; }
		public float DeltaY { get; }

		public SweepEdge( in Edge edge )
		{
			Index = edge.Index;

			Origin = edge.Origin;
			DeltaY = Math.Abs( edge.Tangent.x ) <= 0.0001f
				? 0f : edge.Tangent.y / edge.Tangent.x;
		}

		public float GetEdgeY( float x )
		{
			return Origin.y + DeltaY * (x - Origin.x);
		}
	}

	private int ConnectTwoWay( ref Edge a, ref Edge b )
	{
		ref var prevA = ref _allEdges[a.PrevEdge];
		ref var prevB = ref _allEdges[b.PrevEdge];

		ref var aNew = ref _allEdges[AddEdge( a.Origin, (b.Origin - a.Origin).Normal, a.Distance )];
		ref var bNew = ref _allEdges[AddEdge( b.Origin, (a.Origin - b.Origin).Normal, b.Distance )];

		aNew.Vertices = AddVertices( ref a );
		bNew.Vertices = AddVertices( ref b );

		SimpleConnectEdges( ref prevA, ref aNew );
		SimpleConnectEdges( ref aNew, ref b );

		SimpleConnectEdges( ref prevB, ref bNew );
		SimpleConnectEdges( ref bNew, ref a );

		_activeEdges.Add( aNew.Index );
		_activeEdges.Add( bNew.Index );

		return aNew.Index;
	}

	private int FixUp( ref Edge v, in Edge e )
	{
		var helperInfo = Fill_Helpers[e.Index];

		if ( helperInfo.WasMerge )
		{
			return ConnectTwoWay( ref v, ref _allEdges[helperInfo.Index] );
		}

		return v.Index;
	}

	private void SetHelper( in Edge edge, in Edge helper, bool wasMerge )
	{
		Fill_Helpers[edge.Index] = (helper.Index, wasMerge);
	}

	private void AddSweepEdge( in Edge edge )
	{
		// TODO: could binary search for insertion point

		var origin = edge.Origin;
		Fill_SweepEdges.Add( new SweepEdge( edge ) );
		Fill_SweepEdges.Sort( ( a, b ) =>
			a.GetEdgeY( origin.x ).CompareTo( b.GetEdgeY( origin.x ) ) );
	}

	private void ReplaceSweepEdge( in Edge old, in Edge replacement )
	{
		// TODO: could binary search

		for ( var i = 0; i < Fill_SweepEdges.Count; ++i )
		{
			if ( Fill_SweepEdges[i].Index == old.Index )
			{
				Fill_SweepEdges[i] = new SweepEdge( in replacement );
				break;
			}
		}
	}

	private void RemoveSweepEdge( in Edge edge )
	{
		// TODO: could binary search

		for ( var i = 0; i < Fill_SweepEdges.Count; ++i )
		{
			if ( Fill_SweepEdges[i].Index == edge.Index )
			{
				Fill_SweepEdges.RemoveAt( i );
				break;
			}
		}
	}

	private int FindAboveSweepEdge( in Edge edge )
	{
		// TODO: could binary search

		foreach ( var other in Fill_SweepEdges )
		{
			if ( edge.PrevEdge == other.Index || edge.Index == other.Index )
			{
				continue;
			}

			if ( other.GetEdgeY( edge.Origin.x ) - edge.Origin.y >= 0f )
			{
				return other.Index;
			}
		}

		throw new Exception();
	}

	private void Fill_UpdateExistingVertices()
	{
		_nextAngle = MathF.PI * 0.5f;
		_nextDistance = float.PositiveInfinity;

		if ( !SkipNormals && Math.Abs( _prevAngle - _nextAngle ) >= 0.001f )
		{
			foreach ( var index in _activeEdges )
			{
				ref var edge = ref _allEdges[index];
				edge.Vertices = (-1, -1);

				AddVertices( ref edge, true );
			}
		}

		_prevAngle = _nextAngle;
	}

	private void Fill_SplitIntoMonotonicPolygons()
	{
		Fill_SortedEdges ??= new List<int>();
		Fill_SortedEdges.Clear();

		Fill_SortedEdges.AddRange( _activeEdges );

		Fill_SortedEdges.Sort( ( a, b ) => Compare( _allEdges[a].Origin, _allEdges[b].Origin ) );

		Fill_Helpers ??= new Dictionary<int, (int Index, bool WasMerge)>();
		Fill_Helpers.Clear();

		Fill_SweepEdges ??= new List<SweepEdge>();
		Fill_SweepEdges.Clear();

		// Based on https://www.cs.umd.edu/class/spring2020/cmsc754/Lects/lect05-triangulate.pdf

		// Add pairs of edges to split into x-monotonic polygons

		foreach ( var index in Fill_SortedEdges )
		{
			EnsureCapacity( 4 );

			ref var edge = ref _allEdges[index];
			ref var next = ref _allEdges[edge.NextEdge];
			ref var prev = ref _allEdges[edge.PrevEdge];

			switch ( CategorizeEvent( in prev, in edge, in next ) )
			{
				case SweepEvent.Start:
					AddSweepEdge( in edge );
					SetHelper( in edge, in edge, false );
					break;

				case SweepEvent.End:
					FixUp( ref edge, in prev );
					RemoveSweepEdge( in prev );
					break;

				case SweepEvent.Split:
					{
						ref var above = ref _allEdges[FindAboveSweepEdge( in edge )];
						ref var helper = ref _allEdges[Fill_Helpers[above.Index].Index];
						ref var fixedUp = ref _allEdges[ConnectTwoWay( ref edge, ref helper )];
						AddSweepEdge( in edge );
						SetHelper( in above, in fixedUp, false );
						SetHelper( in edge, in edge, false );
						break;
					}

				case SweepEvent.Merge:
					{
						ref var above = ref _allEdges[FindAboveSweepEdge( in edge )];
						RemoveSweepEdge( in prev );
						ref var new1 = ref _allEdges[FixUp( ref edge, in above )];
						FixUp( ref new1, in prev );
						SetHelper( in above, in new1, true );
						break;
					}

				case SweepEvent.Upper:
					FixUp( ref edge, in prev );
					ReplaceSweepEdge( in prev, in edge );
					SetHelper( in edge, in edge, false );
					break;

				case SweepEvent.Lower:
					{
						ref var above = ref _allEdges[FindAboveSweepEdge( in edge )];
						ref var helper = ref _allEdges[FixUp( ref edge, in above )];
						SetHelper( in above, in helper, false );
						break;
					}
			}
		}
	}

	private readonly struct CloseVertex
	{
		public Vector2 Position { get; }

		/// <summary>
		/// Difference to this vertex from the previous one.
		/// </summary>
		public Vector2 Delta { get; }

		public int Vertex { get; }
		public bool IsUpper { get; }

		public CloseVertex( Vector2 position, Vector2 delta, int vertex, bool isUpper )
		{
			Position = position;
			Delta = delta;
			Vertex = vertex;
			IsUpper = isUpper;
		}
	}

	[ThreadStatic]
	private static List<CloseVertex> Fill_Vertices;

	[ThreadStatic]
	private static Stack<CloseVertex> Fill_Stack;

	private static bool IsReflex( Vector2 prevDelta, Vector2 nextDelta )
	{
		return Vector2.Dot( Helpers.Rotate90( nextDelta ), prevDelta ) >= 0f;
	}

	private static int Compare( Vector2 a, Vector2 b )
	{
		var xCompare = a.x.CompareTo( b.x );
		if ( xCompare != 0 ) return xCompare;
		return a.y.CompareTo( b.y );
	}

	private void Fill_Triangulate()
	{
		Fill_Vertices ??= new List<CloseVertex>();
		Fill_Stack ??= new Stack<CloseVertex>();

		while ( _activeEdges.Count > 0 )
		{
			var firstIndex = _activeEdges.First();
			_activeEdges.Remove( firstIndex );

			var first = _allEdges[firstIndex];

			var minPos = first.Origin;
			var maxPos = first.Origin;
			var minEdgeIndex = firstIndex;
			var maxEdgeIndex = firstIndex;

			var edge = first;

			while ( edge.NextEdge != first.Index )
			{
				edge = _allEdges[edge.NextEdge];
				_activeEdges.Remove( edge.Index );

				if ( Compare( edge.Origin, minPos ) < 0 )
				{
					minPos = edge.Origin;
					minEdgeIndex = edge.Index;
				}

				if ( Compare( edge.Origin, maxPos ) > 0 )
				{
					maxPos = edge.Origin;
					maxEdgeIndex = edge.Index;
				}
			}

			Fill_Vertices.Clear();

			edge = _allEdges[minEdgeIndex];
			Fill_Vertices.Add( new CloseVertex( edge.Origin, default, edge.Vertices.Prev, true ) );

			while ( edge.NextEdge != maxEdgeIndex )
			{
				var next = _allEdges[edge.NextEdge];
				Fill_Vertices.Add( new CloseVertex( next.Origin, next.Origin - edge.Origin, next.Vertices.Prev, true ) );
				edge = next;
			}

			edge = _allEdges[maxEdgeIndex];

			while ( edge.Index != minEdgeIndex )
			{
				var next = _allEdges[edge.NextEdge];
				Fill_Vertices.Add( new CloseVertex( edge.Origin, edge.Origin - next.Origin, edge.Vertices.Prev, false ) );
				edge = next;
			}

			Fill_Vertices.Sort( ( a, b ) => Compare( a.Position, b.Position ) );

			Fill_Stack.Clear();
			Fill_Stack.Push( Fill_Vertices[0] );
			Fill_Stack.Push( Fill_Vertices[1] );

			for ( var i = 2; i < Fill_Vertices.Count; ++i )
			{
				var next = Fill_Vertices[i];
				var top = Fill_Stack.Peek();

				if ( top.IsUpper != next.IsUpper )
				{
					// Case 1

					while ( Fill_Stack.Count > 1 )
					{
						var curr = Fill_Stack.Pop();
						var prev = Fill_Stack.Peek();

						if ( next.IsUpper )
						{
							AddTriangle( next.Vertex, prev.Vertex, curr.Vertex );
						}
						else
						{
							AddTriangle( next.Vertex, curr.Vertex, prev.Vertex );
						}
					}

					Fill_Stack.Clear();
					Fill_Stack.Push( top );
					Fill_Stack.Push( new CloseVertex( next.Position,
						next.Position - top.Position,
						next.Vertex, next.IsUpper ) );
					continue;
				}

				while ( Fill_Stack.Count > 1 && IsReflex( top.Delta, next.Position - top.Position ) != top.IsUpper )
				{
					var curr = Fill_Stack.Pop();
					top = Fill_Stack.Peek();

					if ( next.IsUpper )
					{
						AddTriangle( next.Vertex, curr.Vertex, top.Vertex );
					}
					else
					{
						AddTriangle( next.Vertex, top.Vertex, curr.Vertex );
					}
				}

				Fill_Stack.Push( new CloseVertex( next.Position,
					next.Position - top.Position,
					next.Vertex, next.IsUpper ) );
			}
		}
	}
}