using Sandbox;
using System;

public sealed class NavigationTargetWanderer : Component
{
	[Property]
	public List<GameObject> PotentialTargets { get; set; }

	[RequireComponent]
	NavMeshAgent Agent { get; set; }

	[RequireComponent]
	Rigidbody Body { get; set; }

	[RequireComponent]
	public SkinnedModelRenderer Model { get; set; }

	private Vector3 _currentTarget = Vector3.Zero;

	private TimeSince _timeSinceLastTargetChange = 0;

	protected override void OnEnabled()
	{
		_currentTarget = PotentialTargets[Random.Shared.Next( 0, PotentialTargets.Count )].WorldPosition;
		Agent.MoveTo( _currentTarget );

		NavMeshAgent agent = GetComponent<NavMeshAgent>();

		if ( agent == null )
			return;

		agent.MoveTo( _currentTarget );
	}


	protected override void OnUpdate()
	{
		var dir = Agent.Velocity;
		var forward = WorldRotation.Forward.Dot( dir );
		var sideward = WorldRotation.Right.Dot( dir );

		var angle = MathF.Atan2( sideward, forward ).RadianToDegree().NormalizeDegrees();

		Model.Set( "move_direction", angle );
		Model.Set( "move_speed", Agent.Velocity.Length );
		Model.Set( "move_groundspeed", Agent.Velocity.WithZ( 0 ).Length );
		Model.Set( "move_y", sideward );
		Model.Set( "move_x", forward );
		Model.Set( "move_z", Agent.Velocity.z );

		Model.Set( "wish_x", Agent.WishVelocity.x );
		Model.Set( "wish_y", Agent.WishVelocity.y );
		Model.Set( "wish_z", Agent.WishVelocity.z );

		if ( _timeSinceLastTargetChange > 20f || WorldPosition.WithZ( 0 ).Distance( _currentTarget.WithZ( 0 ) ) < 16f )
		{
			_currentTarget = PotentialTargets[Random.Shared.Next( 0, PotentialTargets.Count )].WorldPosition;
			Agent.MoveTo( _currentTarget );
			_timeSinceLastTargetChange = 0;
		}
	}


}

public sealed class NavigationLinkTraversal : Component
{
	[RequireComponent]
	NavMeshAgent Agent { get; set; }

	[RequireComponent]
	Rigidbody Body { get; set; }

	[RequireComponent]
	public SkinnedModelRenderer Model { get; set; }

	protected override void OnEnabled()
	{
		Agent.AutoTraverseLinks = false;

		NavMeshAgent agent = GetComponent<NavMeshAgent>();

		if ( agent == null )
			return;

		agent.LinkEnter += OnNavLinkEnter;
		agent.LinkExit += OnNavLinkExit;
	}

	protected override void OnDisabled()
	{
		NavMeshAgent agent = GetComponent<NavMeshAgent>();
		if ( agent == null )
			return;

		agent.LinkEnter -= OnNavLinkEnter;
		agent.LinkExit -= OnNavLinkExit;
	}

	private void OnNavLinkEnter( NavMeshLink link )
	{
		Model.Set( "b_grounded", false );
		Model.Set( "b_jump", true );

		// we don't know if we are at start or the end
		// use the closest one as source and the one furthes away as target
		var distanceToStart = WorldPosition.DistanceSquared( link.StartOnNavmesh.Value );
		var distanceToEnd = WorldPosition.DistanceSquared( link.EndOnNavmesh.Value );

		var source = distanceToStart > distanceToEnd ? link.EndOnNavmesh.Value : link.StartOnNavmesh.Value;
		var target = distanceToStart > distanceToEnd ? link.StartOnNavmesh.Value : link.EndOnNavmesh.Value;

		var xyVelocity = Agent.MaxSpeed * (target.WithZ( 0 ) - WorldPosition.WithZ( 0 )).Normal;
		// DEbug start end
		DebugOverlay.Line( WorldPosition, target, Color.Red, 0.5f );
		DebugOverlay.Sphere( new Sphere( WorldPosition, 8 ), Color.Green, 2f );


		var velocity = xyVelocity + Vector3.Up * Math.Max( 450f, (target.z - WorldPosition.z) * 8f );
		DebugOverlay.Text( WorldPosition + Vector3.Up * 10f, velocity.ToString(), 32f, TextFlag.Center, Color.Red, 2f );

		Jump( velocity );
	}

	private void OnNavLinkExit( NavMeshLink link )
	{
		Model.Set( "b_grounded", true );
	}

	protected override void OnUpdate()
	{
		FindGround();
	}

	public void FindGround()
	{
		var tr = Scene.Trace.Ray( WorldPosition + Vector3.Up * 0.1f, WorldPosition + Vector3.Down * 1000 )
			.IgnoreGameObjectHierarchy( GameObject )
			.Run();

		if ( !isGrounded && tr.Hit && nextGround < 0 && tr.Distance < 4f )
		{
			Log.Info( "Grounded" );
			isGrounded = true;
			Model.Set( "b_grounded", true );
			Agent.UpdatePosition = true;
			Agent.SetAgentPosition( tr.EndPosition );
			Agent.CompleteLink();
		}
	}

	private TimeUntil nextGround = 0;
	private bool isGrounded = false;

	public void Jump( Vector3 velocity )
	{
		nextGround = 0.5f;
		isGrounded = false;
		Agent.UpdatePosition = false;

		Body.Velocity = velocity;
	}
}
