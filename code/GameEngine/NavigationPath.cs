using Sandbox;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using static Sandbox.NavigationMesh;

public class NavigationPath
{
	public Vector3 StartPoint;
	public Vector3 EndPoint;
	private NavigationMesh navigationMesh;

	public NavigationMesh.Node StartNode;
	public NavigationMesh.Node EndNode;

	public double Milliseconds;

	public List<Vector3> PointList = new List<Vector3>( 128 );

	public NavigationPath( NavigationMesh navigationMesh )
	{
		this.navigationMesh = navigationMesh;


	}

	record struct ScoredNode( Node Node, float Score );

	public List<Vector3> Build()
	{


		PointList.Clear();

		// clean up
		foreach ( var n in navigationMesh.areas.Values )
		{
			n.ResetPath();
		}

		HashSet<Node> open = new HashSet<Node>( 64 );

		StartNode = FindClosestNode( StartPoint );
		EndNode = FindClosestNode( EndPoint );

		var sw = Stopwatch.StartNew();


		if ( StartNode == EndNode )
		{
			// Build trivial path
			return PointList;
		}
		StartNode.ResetPath();
		EndNode.ResetPath();

		open.Add( StartNode );
		StartNode.Path.Position = StartPoint;

		while ( open.Any() )
		{
			Node node = open.OrderBy( x => x.Path.Score ).First();
			open.Remove( node );
			node.Path.Closed = true;

			float currentCost = node.Path.DistanceFromStart;

			if ( node == EndNode )
			{
				break;
			}

			foreach( var connection in node.Connections )
			{
				if ( connection.Target.Path.Closed ) continue;

				var score = currentCost + ScoreFor( node.Path.Position, connection, out var position );
				var distanceFromParent = position.Distance( node.Path.Position );
				score += distanceFromParent;

				if ( connection.Target.Path.Parent is not null && score < connection.Target.Path.Score ) continue;

				connection.Target.Path.Score = score;
				connection.Target.Path.Parent = node;
				connection.Target.Path.Connection = connection;
				connection.Target.Path.Position = position;
				connection.Target.Path.DistanceFromStart = currentCost + distanceFromParent;
				open.Add( connection.Target );
			}
		}

		Gizmo.Draw.Color = Color.Green;
		
		var p = EndNode;
		PointList.Add( EndPoint );
		while ( p is not null )
		{
			PointList.Add( p.Path.Position );
			p = p.Path.Parent;
		}
		PointList.Reverse();

		Milliseconds = sw.Elapsed.TotalMilliseconds;
		return PointList;
	}

	private float ScoreFor( in Vector3 parentPosition, in Node.Connection connection, out Vector3 position )
	{
		var line = new Line( connection.Left, connection.Right );
		var center = line.ClosestPoint( parentPosition );

		position = center;

		var g = EndNode.Center.Distance( center );

		return g;
	}

	enum PathResult
	{
		FAIL,
		AT_END
	}

	NavigationMesh.Node FindClosestNode( Vector3 position )
	{
		// slow - we can use a spatial hash to speed this up
		return navigationMesh.areas.Values.OrderBy( x => x.Center.DistanceSquared( position ) ).FirstOrDefault();
	}
}
