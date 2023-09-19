using Sandbox;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

public class NavigationPath
{
	/// <summary>
	/// When enabled (default) will remove path nodes that are very close to each other.
	/// </summary>
	public bool AllowPathSimplify { get; set; } = true;

	/// <summary>
	/// When enabled (default) will smooth the path out
	/// </summary>
	public bool AllowPathSmoothing { get; set; } = true;

	/// <summary>
	/// Start position
	/// </summary>
	public Vector3 StartPoint { get; set; }

	/// <summary>
	/// Goal position
	/// </summary>
	public Vector3 EndPoint { get; set; }

	/// <summary>
	/// How manuy milliseconds the last geneation took
	/// </summary>
	public double GenerationMilliseconds { get; set; }

	/// <summary>
	/// The individual segments of the path
	/// </summary>
	public List<Segment> Segments = new List<Segment>( 256 );

	private NavigationMesh navigationMesh;
	private NavigationMesh.Node startNode;
	private NavigationMesh.Node endNode;

	public NavigationPath( NavigationMesh navigationMesh )
	{
		this.navigationMesh = navigationMesh;
	}

	public void Build()
	{
		var sw = Stopwatch.StartNew();

		//	AllowPathSmoothing = RealTime.Now % 2 > 1;
		Segments.Clear();

		// clean up
		foreach ( var n in navigationMesh.areas.Values )
		{
			n.ResetPath();
		}

		HashSet<NavigationMesh.Node> open = new HashSet<NavigationMesh.Node>( 64 );

		// todo - sppeed up using 
		startNode = FindClosestNode( StartPoint );
		endNode = FindClosestNode( EndPoint );

		if ( startNode == endNode )
		{
			// Build trivial path
			return;
		}
		startNode.ResetPath();
		endNode.ResetPath();

		open.Add( startNode );
		startNode.Path.Position = startNode.ClosestPoint( StartPoint );

		while ( open.Any() )
		{
			NavigationMesh.Node node = open.OrderBy( x => x.Path.Score ).First();
			open.Remove( node );
			node.Path.Closed = true;

			float currentCost = node.Path.DistanceFromStart;

			if ( node == endNode )
			{
				FinishBuilding();
				break;
			}

			foreach( var connection in node.Connections )
			{
				if ( connection.Target.Path.Closed ) continue;

				var score = ScoreFor( node.Path.Position, connection, out var position );
				var distanceFromParent = position.Distance( node.Path.Position );
				score += distanceFromParent + currentCost;

				if ( connection.Target.Path.Parent is not null && score > connection.Target.Path.Score ) continue;

				connection.Target.Path.Score = score;
				connection.Target.Path.Parent = node;
				connection.Target.Path.Connection = connection;
				connection.Target.Path.Position = position;
				connection.Target.Path.DistanceFromStart = currentCost + distanceFromParent;
				open.Add( connection.Target );
			}
		}

		GenerationMilliseconds = sw.Elapsed.TotalMilliseconds;
	}

	void FinishBuilding()
	{
		var p = endNode;
		Vector3 previousPosition = endNode.ClosestPoint( EndPoint );

		Segments.Add( new Segment { Node = p, Position = previousPosition } );

		while ( p is not null )
		{
			var s = new Segment();
			s.Node = p;
			s.Position = p.Path.Position;
			s.Distance = p.Path.DistanceFromStart;
			s.Connection = p.Path.Connection;

			if ( p.Path.Parent is NavigationMesh.Node next )
			{
				var nextPos = next.Path.Position;

				if ( AllowPathSimplify && Vector3.DistanceBetween( previousPosition, nextPos ) < 64 && Vector3.DistanceBetween( previousPosition, s.Position ) < 64 )
					goto next;

				if ( AllowPathSmoothing )
				{
					previousPosition = (previousPosition + nextPos) * 0.5f;
					s.Position = p.Path.Connection.Line.ClosestPoint( previousPosition );
				}
			}

			previousPosition = s.Position;

			Segments.Add( s );

			next:
			p = p.Path.Parent;
		}
		Segments.Reverse();

		if ( AllowPathSmoothing )
		{
			for ( int iteration = 0; iteration < 4; iteration++ )
				for ( int i = 1; i < Segments.Count - 1; i++ )
				{
					var before = Segments[i - 1].Position;
					var after = Segments[i + 1].Position;

					if ( Segments[i].Connection.Line.ClosestPoint( new Ray( before, (after - before).Normal ), out var pol ) )
					{
						Segments[i].Position = pol;
					}
				}
		}
	}

	private float ScoreFor( in Vector3 parentPosition, in NavigationMesh.Node.Connection connection, out Vector3 position )
	{
		position = connection.Line.Center;

		var g = EndPoint.Distance( position );

		return g;
	}

	NavigationMesh.Node FindClosestNode( Vector3 position )
	{
		// slow - we can use a spatial hash to speed this up
		return navigationMesh.areas.Values.Where( x => x.Center.z < position.z + 64 ).OrderBy( x => x.Distance( position ) ).FirstOrDefault();
	}

	public class Segment
	{
		public NavigationMesh.Node Node;
		public NavigationMesh.Node.Connection Connection;
		public Vector3 Position;
		public float Distance;
	}
}
