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
	}

	protected override void OnDisabled()
	{
		NavMeshAgent agent = GetComponent<NavMeshAgent>();
		if ( agent == null )
			return;

		agent.LinkEnter -= OnNavLinkEnter;
	}

	private void OnNavLinkEnter()
	{
		// If link is a ladder and we are going up, climb it
		if ( Agent.CurrentLinkTraversal.Value.LinkComponent.Tags.Has( "ladder" ) &&
			Agent.CurrentLinkTraversal.Value.LinkExitPosition.z > WorldPosition.z )
		{
			ClimbLadder();
		}
		else
		{
			// 50/50 chance of physics jump or paraobolic jump
			if ( Random.Shared.Next( 0, 2 ) == 0 )
				PhysicsJump();
			else
				ParabolicJump();
		}
	}

	private async void ClimbLadder()
	{
		var initialPos = Agent.CurrentLinkTraversal.Value.AgentInitialPosition;

		var start = Agent.CurrentLinkTraversal.Value.LinkEnterPosition;
		var endVertical = start.WithZ( Agent.CurrentLinkTraversal.Value.LinkExitPosition.z );
		var end = Agent.CurrentLinkTraversal.Value.LinkExitPosition;

		var climbSpeed = 100f;

		var startDuration = (start - initialPos).Length / climbSpeed;
		var climbDuration = (endVertical - start).Length / climbSpeed;
		var endDuration = (end - endVertical).Length / climbSpeed;

		var totalLadderTime = startDuration + climbDuration + endDuration;

		TimeSince timeSinceStart = 0;

		while ( timeSinceStart < totalLadderTime )
		{
			Vector3 newPosition = start;

			// 1. Make sure we are positioned at the link start
			if ( timeSinceStart < startDuration )
			{
				newPosition = Vector3.Lerp( initialPos, start, timeSinceStart / startDuration );
			}
			// 2. Vertical Movement
			else if ( timeSinceStart < startDuration + climbDuration )
			{
				newPosition = Vector3.Lerp( start, endVertical, (timeSinceStart - startDuration) / climbDuration );
			}
			// 3. Move off ladder to link end position
			else
			{
				newPosition = Vector3.Lerp( endVertical, end, (timeSinceStart - startDuration - climbDuration) / endDuration );
			}

			Agent.SetAgentPosition( newPosition );

			await Task.Frame();
		}

		Agent.SetAgentPosition( end );

		Agent.CompleteLinkTraversal();
	}

	private async void ParabolicJump()
	{
		Model.Set( "b_grounded", false );
		Model.Set( "b_jump", true );

		var start = Agent.CurrentLinkTraversal.Value.AgentInitialPosition;
		var end = Agent.CurrentLinkTraversal.Value.LinkExitPosition;

		// Calculate peak height for the parabolic arc
		var heightDifference = end.z - start.z;
		var peakHeight = MathF.Abs( heightDifference ) + 25f;

		var mid = (start + end) / 2f;

		// Estimate prabolic duraion size using a triangle /\ between start, mid, end 
		var startToMid = mid.WithZ( peakHeight ) - start;
		var midToEnd = end - mid.WithZ( peakHeight );
		var duration = ( startToMid + midToEnd ).Length / Agent.MaxSpeed;
		duration = MathF.Max( 0.75f, duration ); // Ensure minimum duration

		TimeSince timeSinceStart = 0;

		while ( timeSinceStart < duration )
		{
			var t = timeSinceStart / duration;

			// Linearly interpolate XY positions
			var newPosition = Vector3.Lerp( start, end, t );

			// Apply parabolic curve to Z position using a quadratic function
			var yOffset = 4f * peakHeight * t * (1f - t);
			newPosition.z = MathX.Lerp( start.z, end.z, t ) + yOffset;

			Agent.SetAgentPosition( newPosition );

			await Task.Frame();
		}

		Agent.SetAgentPosition( end );

		Model.Set( "b_grounded", true );
		Model.Set( "b_jump", false );

		Agent.CompleteLinkTraversal();
	}


	private async void PhysicsJump()
	{
		Model.Set( "b_grounded", false );
		Model.Set( "b_jump", true );

		// Physiscs will drive our jump so disable game object position sync
		Agent.UpdatePosition = false;

		var start = Agent.CurrentLinkTraversal.Value.AgentInitialPosition;
		var end = Agent.CurrentLinkTraversal.Value.LinkExitPosition;

		var xyVelocity = Agent.MaxSpeed * (end.WithZ( 0 ) - start.WithZ( 0 )).Normal * 1.25f; 

		var velocity = xyVelocity + Vector3.Up * Math.Max( 500f, (end.z - start.z) * 8f );
		// Launch the agent into the air
		Body.Velocity = velocity;

		TimeSince timeSinceStart = 0;

		while ( true )
		{
			Agent.SetAgentPosition( WorldPosition );

			// Try to find ground
			var tr = Scene.Trace.Ray( WorldPosition + Vector3.Up * 0.1f, WorldPosition + Vector3.Down * 1000 )
				.IgnoreGameObjectHierarchy( GameObject )
				.Run();

			if ( tr.Hit && timeSinceStart > 0.5f && tr.Distance < 4f )
			{
				break;
			}

			await Task.Frame();
		}

		Agent.SetAgentPosition( WorldPosition );

		// Hand back position control to the agent
		Agent.UpdatePosition = true;

		Model.Set( "b_grounded", true );
		Model.Set( "b_jump", false );

		Agent.CompleteLinkTraversal();
	}
}
