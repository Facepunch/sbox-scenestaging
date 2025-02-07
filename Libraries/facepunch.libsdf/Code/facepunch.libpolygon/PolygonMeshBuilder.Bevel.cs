using System;
using System.Collections.Generic;
using System.Linq;

namespace Sandbox.Polygons;

partial class PolygonMeshBuilder
{
	private HashSet<(int A, int B)> PossibleCuts { get; } = new();

	[ThreadStatic] private static List<(int A, int B)> Bevel_PossibleCutList;

	[ThreadStatic] private static List<int> Bevel_ActiveEdgeList;


	/// <summary>
	/// Add faces starting at each active edge, traveling inwards and upwards to produce a bevel.
	/// If the bevel distance is large enough the mesh will become closed. Otherwise, you can use
	/// <see cref="Fill"/> to add a flat face after the bevel.
	/// </summary>
	/// <param name="width">Total distance inwards.</param>
	/// <param name="height">Total distance upwards, away from the plane of the polygon.</param>
	public PolygonMeshBuilder Bevel( float width, float height )
	{
		var angle = MathF.Atan2( width, height );

		return Bevel( width, height, angle, angle );
	}

	/// <summary>
	/// Add faces starting at each active edge, traveling inwards and upwards to produce a bevel.
	/// Use <paramref name="prevAngle"/> and <paramref name="nextAngle"/> to control the normal directions
	/// at the start and end of the bevel faces. Angles are in radians, with 0 pointing outwards along
	/// the plane of the polygon, and PI/2 pointing upwards away from the plane.
	/// If the bevel distance is large enough the mesh will become closed. Otherwise, you can use
	/// <see cref="Fill"/> to add a flat face after the bevel.
	/// </summary>
	/// <param name="width">Total distance inwards.</param>
	/// <param name="height">Total distance upwards, away from the plane of the polygon.</param>
	/// <param name="prevAngle">Angle, in radians, to use for normals at the outside of the bevel.</param>
	/// <param name="nextAngle"></param>
	public PolygonMeshBuilder Bevel( float width, float height, float prevAngle, float nextAngle )
	{
		if ( width < 0f )
		{
			throw new ArgumentOutOfRangeException( nameof( width ) );
		}

		Validate();
		Bevel_UpdateExistingVertices( width, height, prevAngle, nextAngle );

		var cutList = Bevel_PossibleCutList ??= new List<(int A, int B)>();
		var edgeList = Bevel_ActiveEdgeList ??= new List<int>();

		var finished = false;
		var endDist = _nextDistance;

		if ( MathF.Abs( _nextDistance ) > 0.001f )
		{
			var maxIterations = _activeEdges.Count * _activeEdges.Count;
			var maxEdges = _nextEdgeIndex + _activeEdges.Count * 4;

			// Find each event as we sweep inwards with all the active edges

			int iterations;
			for ( iterations = 0; iterations < maxIterations && _activeEdges.Count > 0 && _nextEdgeIndex <= maxEdges; ++iterations )
			{
				int? closedEdge = null;
				int? splitEdge = null;
				int? splittingEdge = null;

				Vector2 bestPos = default;

				var bestDist = _nextDistance;
				var bestMerge = false;

				// Are any edges closing (reducing down to a point)?

				foreach ( var index in _activeEdges )
				{
					ref var edge = ref _allEdges[index];

					if ( edge.MaxDistance >= bestDist ) continue;

					var next = _allEdges[edge.NextEdge];

					bestDist = edge.MaxDistance;
					closedEdge = edge.Index;
					bestPos = (edge.Project( edge.MaxDistance ) + next.Project( edge.MaxDistance )) * 0.5f;
				}

				cutList.Clear();
				cutList.AddRange( PossibleCuts );

				// Are any edges being cut by a vertex?

				foreach ( var (index, otherIndex) in cutList )
				{
					if ( !_activeEdges.Contains( index ) || !_activeEdges.Contains( otherIndex ) )
					{
						PossibleCuts.Remove( (index, otherIndex) );
						continue;
					}

					var edge = _allEdges[index];
					var other = _allEdges[otherIndex];
					var otherNext = _allEdges[other.NextEdge];

					var splitDist = CalculateSplitDistance( edge, other, otherNext,
						out var splitPos, out var merge );

					if ( splitDist - _nextDistance > 0.001f )
					{
						PossibleCuts.Remove( (index, otherIndex) );
						continue;
					}

					if ( splitDist >= bestDist ) continue;

					bestDist = splitDist;
					bestPos = splitPos;
					bestMerge = merge != MergeMode.None;

					closedEdge = null;
					splittingEdge = edge.Index;

					splitEdge = merge == MergeMode.End ? otherNext.Index : other.Index;
				}

				if ( splittingEdge != null && bestMerge )
				{
					Bevel_Merge( splittingEdge.Value, splitEdge.Value, bestPos, bestDist );
					continue;
				}

				if ( splittingEdge != null )
				{
					Bevel_Split( splittingEdge.Value, splitEdge.Value, bestPos, bestDist );
					continue;
				}

				if ( closedEdge != null )
				{
					Bevel_Close( closedEdge.Value, bestPos, bestDist );
					continue;
				}

				finished = true;
				break;
			}

			if ( _activeEdges.Count > 0 && iterations == maxIterations || _nextEdgeIndex > maxEdges )
			{
				throw new Exception( $"Exploded after {iterations} with {_activeEdges.Count} active edges!" );
			}
		}
		else
		{
			finished = true;
		}

		if ( !finished && _activeEdges.Count > 0 )
		{
			endDist = _activeEdges.Max( i => _allEdges[i].Distance );
		}

		EnsureCapacity( _activeEdges.Count );

		edgeList.Clear();
		edgeList.AddRange( _activeEdges );

		_activeEdges.Clear();

		foreach ( var index in edgeList )
		{
			ref var b = ref _allEdges[index];
			ref var a = ref _allEdges[b.PrevEdge];
			ref var c = ref _allEdges[b.NextEdge];
			ref var d = ref _allEdges[AddEdge( b.Project( endDist ), b.Tangent, endDist )];

			var ai = AddVertices( ref a );
			var bi = AddVertices( ref b );
			var ci = AddVertices( ref c );

			ConnectEdges( ref a, ref d );
			ConnectEdges( ref d, ref c );

			var di = AddVertices( ref d, true );

			AddTriangle( ai.Next, di.Prev, bi.Prev );
			AddTriangle( bi.Next, di.Next, ci.Prev );

			_activeEdges.Add( d.Index );
		}

		PostBevel();

		return this;
	}

	private void Bevel_UpdateExistingVertices( float width, float height, float prevAngle, float nextAngle )
	{
		_nextDistance = _prevDistance + width;
		_nextHeight = _prevHeight + height;
		_nextAngle = nextAngle;
		_minSmoothNormalDot = MathF.Cos( Math.Clamp( MaxSmoothAngle, 0f, MathF.PI * (511f / 512f) ) );

		_invDistance = width <= 0.0001f ? 0f : 1f / (_nextDistance - _prevDistance);

		if ( !SkipNormals && Math.Abs( _prevAngle - prevAngle ) >= 0.001f )
		{
			foreach ( var index in _activeEdges )
			{
				ref var edge = ref _allEdges[index];
				edge.Vertices = (-1, -1);
			}
		}

		_prevAngle = prevAngle;

		PossibleCuts.Clear();

		foreach ( var index in _activeEdges )
		{
			ref var edge = ref _allEdges[index];
			UpdateMaxDistance( ref edge, _allEdges[edge.NextEdge] );

			foreach ( var otherIndex in _activeEdges )
			{
				if ( otherIndex != index )
				{
					PossibleCuts.Add( (index, otherIndex) );
				}
			}
		}
	}

	private void Bevel_Merge( int edgeA, int edgeB, Vector2 mergePos, float bestDist )
	{
		EnsureCapacity( 2 );

		ref var a = ref _allEdges[edgeA];
		ref var b = ref _allEdges[edgeB];

		_activeEdges.Remove( a.Index );
		_activeEdges.Remove( b.Index );

		if ( a.NextEdge == b.Index && b.NextEdge == a.Index )
		{
			return;
		}

		ref var aPrev = ref _allEdges[a.PrevEdge];
		ref var bPrev = ref _allEdges[b.PrevEdge];

		ref var aNext = ref _allEdges[a.NextEdge];
		ref var bNext = ref _allEdges[b.NextEdge];

		ref var aNew = ref _allEdges[AddEdge( mergePos, a.Tangent, bestDist, 1 )];
		ref var bNew = ref _allEdges[AddEdge( mergePos, b.Tangent, bestDist, -1 )];

		var aPrevi = AddVertices( ref aPrev ).Next;
		var ai = AddVertices( ref a );
		var aNexti = AddVertices( ref aNext ).Prev;
		var bPrevi = AddVertices( ref bPrev ).Next;
		var bi = AddVertices( ref b );
		var bNexti = AddVertices( ref bNext ).Prev;

		_activeEdges.Add( aNew.Index );
		_activeEdges.Add( bNew.Index );

		ConnectEdges( ref bPrev, ref aNew );
		ConnectEdges( ref aNew, ref aNext );

		ConnectEdges( ref aPrev, ref bNew );
		ConnectEdges( ref bNew, ref bNext );

		UpdateMaxDistance( ref bPrev, aNew );
		UpdateMaxDistance( ref aNew, aNext );
		UpdateMaxDistance( ref aNext, _allEdges[aNext.NextEdge] );

		UpdateMaxDistance( ref aPrev, bNew );
		UpdateMaxDistance( ref bNew, bNext );
		UpdateMaxDistance( ref bNext, _allEdges[bNext.NextEdge] );

		var aNewi = AddVertices( ref aNew );
		var bNewi = AddVertices( ref bNew );

		AddTriangle( aPrevi, bNewi.Prev, ai.Prev );
		AddTriangle( ai.Next, aNewi.Next, aNexti );
		AddTriangle( bPrevi, aNewi.Prev, bi.Prev );
		AddTriangle( bi.Next, bNewi.Next, bNexti );

		AddAllPossibleCuts( aNew.Index );
		AddAllPossibleCuts( aNext.Index );
		AddAllPossibleCuts( bNew.Index );
		AddAllPossibleCuts( bNext.Index );
	}

	private void Bevel_Split( int splittingEdge, int splitEdge, Vector2 splitPos, float bestDist )
	{
		EnsureCapacity( 2 );

		ref var a = ref _allEdges[splitEdge];
		ref var d = ref _allEdges[splittingEdge];
		ref var b = ref _allEdges[AddEdge( splitPos, a.Tangent, bestDist, 1 )];
		ref var c = ref _allEdges[d.PrevEdge];
		ref var e = ref _allEdges[AddEdge( splitPos, d.Tangent, bestDist, -1 )];
		ref var aNext = ref _allEdges[a.NextEdge];
		ref var dNext = ref _allEdges[d.NextEdge];

		var ai = AddVertices( ref a ).Next;
		var fi = AddVertices( ref aNext ).Prev;
		var ci = AddVertices( ref c ).Next;
		var di = AddVertices( ref d );
		var gi = AddVertices( ref dNext ).Prev;

		_activeEdges.Remove( d.Index );
		_activeEdges.Add( b.Index );
		_activeEdges.Add( e.Index );

		ConnectEdges( ref a, ref e );
		ConnectEdges( ref e, ref dNext );

		ConnectEdges( ref c, ref b );
		ConnectEdges( ref b, ref aNext );

		UpdateMaxDistance( ref a, e );
		UpdateMaxDistance( ref e, dNext );
		UpdateMaxDistance( ref dNext, _allEdges[dNext.NextEdge] );

		UpdateMaxDistance( ref c, b );
		UpdateMaxDistance( ref b, aNext );
		UpdateMaxDistance( ref aNext, _allEdges[aNext.NextEdge] );

		var bi = AddVertices( ref b );
		var ei = AddVertices( ref e );

		AddTriangle( ai, bi.Next, fi );
		AddTriangle( ci, bi.Prev, di.Prev );
		AddTriangle( di.Next, ei.Next, gi );

		AddAllPossibleCuts( b.Index );
		AddAllPossibleCuts( dNext.Index );
		AddAllPossibleCuts( e.Index );
		AddAllPossibleCuts( aNext.Index );
	}

	private void Bevel_Close( int closedEdge, Vector2 closePos, float bestDist )
	{
		EnsureCapacity( 1 );

		ref var b = ref _allEdges[closedEdge];
		ref var a = ref _allEdges[b.PrevEdge];
		ref var c = ref _allEdges[b.NextEdge];
		ref var cNext = ref _allEdges[c.NextEdge];
		ref var d = ref _allEdges[AddEdge( closePos, c.Tangent, bestDist )];

		_activeEdges.Remove( b.Index );
		_activeEdges.Remove( c.Index );

		if ( b.PrevEdge == b.NextEdge )
		{
			return;
		}

		_activeEdges.Add( d.Index );

		ConnectEdges( ref a, ref d );
		ConnectEdges( ref d, ref cNext );

		UpdateMaxDistance( ref a, d );
		UpdateMaxDistance( ref d, cNext );
		UpdateMaxDistance( ref cNext, _allEdges[cNext.NextEdge] );

		var ai = AddVertices( ref a );
		var bi = AddVertices( ref b );
		var ci = AddVertices( ref c );
		var ei = AddVertices( ref cNext );
		var di = AddVertices( ref d );

		var fi = _vertices.Count;

		_vertices.Add( new(
			_vertices[di.Prev].Position,
			_vertices[bi.Next].Normal,
			_vertices[bi.Next].Tangent ) );

		AddTriangle( ai.Next, di.Prev, bi.Prev );
		AddTriangle( bi.Next, fi, ci.Prev );
		AddTriangle( ci.Next, di.Next, ei.Prev );

		AddAllPossibleCuts( d.Index );
		AddAllPossibleCuts( cNext.Index );
	}

	private void PostBevel()
	{
		_prevDistance = _nextDistance;
		_prevHeight = _nextHeight;
		_prevAngle = _nextAngle;
	}

	private void AddAllPossibleCuts( int index )
	{
		foreach ( var otherIndex in _activeEdges )
		{
			if ( otherIndex != index )
			{
				PossibleCuts.Add( (index, otherIndex) );
				PossibleCuts.Add( (otherIndex, index) );
			}
		}
	}

	private static Vector3 RotateNormal( Vector3 oldNormal, float sin, float cos )
	{
		var normal2d = new Vector2( oldNormal.x, oldNormal.y );

		if ( normal2d.LengthSquared <= 0.000001f )
		{
			return oldNormal;
		}

		normal2d = normal2d.Normal;

		return new Vector3( normal2d.x * cos, normal2d.y * cos, sin ).Normal;
	}

	private static float GetEpsilon( Vector2 vec, float frac = 0.0001f )
	{
		return Math.Max( Math.Abs( vec.x ), Math.Abs( vec.y ) ) * frac;
	}

	private static float GetEpsilon( Vector2 a, Vector2 b, float frac = 0.0001f )
	{
		return Math.Max( GetEpsilon( a ), GetEpsilon( b ) );
	}

	private static float GetEpsilon( Vector2 a, Vector2 b, Vector3 c, float frac = 0.0001f )
	{
		return Math.Max( GetEpsilon( a ), Math.Max( GetEpsilon( b ), GetEpsilon( c ) ) );
	}

	private static void UpdateMaxDistance( ref Edge edge, in Edge nextEdge )
	{
		if ( edge.NextEdge == edge.PrevEdge )
		{
			edge.MaxDistance = edge.Distance;
			return;
		}

		var baseDistance = Math.Max( edge.Distance, nextEdge.Distance );
		var thisOrigin = edge.Project( baseDistance );
		var nextOrigin = nextEdge.Project( baseDistance );

		var posDist = Vector2.Dot( nextOrigin - thisOrigin, edge.Tangent );

		var dPrev = Vector2.Dot( edge.Velocity, edge.Tangent );
		var dNext = Vector2.Dot( nextEdge.Velocity, edge.Tangent );

		if ( dPrev - dNext <= 0.001f )
		{
			var epsilon = GetEpsilon( thisOrigin, nextOrigin, 0.001f );
			edge.MaxDistance = posDist <= epsilon ? baseDistance : float.PositiveInfinity;
		}
		else
		{
			edge.MaxDistance = baseDistance + MathF.Max( 0f, posDist / (dPrev - dNext) );
		}
	}

	private static void SimpleConnectEdges( ref Edge prev, ref Edge next )
	{
		prev.NextEdge = next.Index;
		next.PrevEdge = prev.Index;
	}

	private static void ConnectEdges( ref Edge prev, ref Edge next )
	{
		SimpleConnectEdges( ref prev, ref next );

		var sum = prev.Normal + next.Normal;
		var sqrMag = sum.LengthSquared;

		if ( sqrMag < 0.001f )
		{
			next.Velocity = Vector2.Zero;
		}
		else
		{
			next.Velocity = 2f * sum / sum.LengthSquared;
		}
	}

	private enum MergeMode
	{
		None,
		Start,
		End
	}

	/// <summary>
	/// Find when the start vertex of <paramref name="edge"/> would cut <paramref name="other"/>.
	/// </summary>
	private static float CalculateSplitDistance( in Edge edge, in Edge other, in Edge otherNext,
		out Vector2 splitPos, out MergeMode merge )
	{
		splitPos = default;
		merge = MergeMode.None;

		if ( other.Index == edge.Index || edge.Twin == other.Index || edge.Velocity.LengthSquared <= 0f )
		{
			return float.PositiveInfinity;
		}

		var dv0 = Vector2.Dot( other.Velocity - edge.Velocity, other.Normal );
		var dv1 = Vector2.Dot( otherNext.Velocity - edge.Velocity, other.Normal );

		if ( Math.Min( dv0, dv1 ) <= GetEpsilon( edge.Velocity, other.Velocity, otherNext.Velocity ) )
		{
			return float.PositiveInfinity;
		}

		var baseDistance = Math.Max( edge.Distance, Math.Max( other.Distance, otherNext.Distance ) );
		var edgeOrigin = edge.Project( baseDistance );
		var otherOrigin = other.Project( baseDistance );
		var otherNextOrigin = otherNext.Project( baseDistance );

		var dx0 = Vector2.Dot( edgeOrigin - otherOrigin, other.Normal );
		var dx1 = Vector2.Dot( edgeOrigin - otherNextOrigin, other.Normal );

		if ( Math.Min( dx0, dx1 ) <= -GetEpsilon( edgeOrigin, otherOrigin, otherNextOrigin ) )
		{
			return float.PositiveInfinity;
		}

		var t0 = dx0 / dv0;
		var t1 = dx1 / dv1;

		var t = Math.Min( t0, t1 );

		if ( t < 0f )
		{
			return float.PositiveInfinity;
		}

		if ( baseDistance + t >= edge.MaxDistance || baseDistance + t >= other.MaxDistance )
		{
			return float.PositiveInfinity;
		}

		splitPos = edgeOrigin + edge.Velocity * t;

		var prevPos = otherOrigin + other.Velocity * t0;
		var nextPos = otherNextOrigin + otherNext.Velocity * t1;

		var dPrev = Vector2.Dot( splitPos - prevPos, other.Tangent );
		var dNext = Vector2.Dot( splitPos - nextPos, other.Tangent );

		var epsilon = GetEpsilon( prevPos, nextPos );

		if ( dPrev <= -epsilon || dNext >= 0f )
		{
			return float.PositiveInfinity;
		}

		if ( dPrev <= epsilon )
		{
			if ( edge.NextEdge == other.Index || edge.PrevEdge == other.Index )
			{
				return float.PositiveInfinity;
			}

			merge = MergeMode.Start;
		}
		else if ( dNext >= -epsilon )
		{
			if ( edge.NextEdge == otherNext.Index || edge.PrevEdge == otherNext.Index )
			{
				return float.PositiveInfinity;
			}

			merge = MergeMode.End;
		}

		return baseDistance + t;
	}
}
