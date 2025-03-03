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

	private void OnNavLinkEnter()
	{
		// We will drive GO animation ourlselves
		Agent.UpdatePosition = false;

		if ( Agent.CurrentLinkData.Value.LinkComponent.Tags.Has( "ladder" ) && Agent.CurrentLinkData.Value.LinkEndPosition.z > WorldPosition.z )
		{
			ClimbLadder();
		}
		else
		{
			// 50/50 chance of jumping or physics jump
			if ( Random.Shared.Next( 0, 2 ) == 0 )
				PhysicsJump();
			else
				ParabolicJump();
		}
	}

	private void OnNavLinkExit()
	{
		// Let the agent drive the GO position
		Agent.UpdatePosition = true;
	}

	protected override void OnUpdate()
	{
	}

	private async void ClimbLadder()
	{
		var initialPos = Agent.CurrentLinkData.Value.AgentInitialPosition;

		var start = Agent.CurrentLinkData.Value.LinkStartPosition;
		var endVertical = start.WithZ( Agent.CurrentLinkData.Value.LinkEndPosition.z );
		var end = Agent.CurrentLinkData.Value.LinkEndPosition;

		var startTime = (start - initialPos).Length / 100f;
		var ladderTime = (endVertical - start).Length / 100f;
		var endTime = (end - endVertical).Length / 100f;

		var totalLadderTime = startTime + ladderTime + endTime;

		TimeSince timeSinceStart = 0;

		var previousPosition = WorldPosition;

		while ( timeSinceStart < totalLadderTime )
		{
			if ( timeSinceStart < startTime )
			{
				WorldPosition = Vector3.Lerp( initialPos, start, timeSinceStart / startTime );
			}
			else if ( timeSinceStart < startTime + ladderTime )
			{
				WorldPosition = Vector3.Lerp( start, endVertical, (timeSinceStart - startTime) / ladderTime );
			}
			else
			{
				WorldPosition = Vector3.Lerp( endVertical, end, (timeSinceStart - startTime - ladderTime) / endTime );
			}

			Agent.Velocity = (WorldPosition - previousPosition) / Time.Delta;
			previousPosition = WorldPosition;

			await Task.Frame();
		}

		WorldPosition = end;

		Agent.CompleteLink();
	}

	private async void ParabolicJump()
	{
		Model.Set( "b_grounded", false );
		Model.Set( "b_jump", true );

		var start = Agent.CurrentLinkData.Value.AgentInitialPosition;
		var end = Agent.CurrentLinkData.Value.LinkEndPosition;

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
			var position = Vector3.Lerp( start, end, t );

			// Apply parabolic curve to Z position using a quadratic function
			var yOffset = 4f * peakHeight * t * (1f - t);
			position.z = MathX.Lerp( start.z, end.z, t ) + yOffset;

			// Update position and velocity
			Agent.Velocity = (position - WorldPosition) / Time.Delta;
			WorldPosition = position;

			await Task.Frame();
		}

		WorldPosition = end;

		Model.Set( "b_grounded", true );
		Model.Set( "b_jump", false );

		Agent.CompleteLink();
	}

	private TimeUntil nextGround = 0;
	private bool isGrounded = false;

	private async void PhysicsJump()
	{
		Model.Set( "b_grounded", false );
		Model.Set( "b_jump", true );

		var start = Agent.CurrentLinkData.Value.AgentInitialPosition;
		var end = Agent.CurrentLinkData.Value.LinkEndPosition;

		var xyVelocity = Agent.MaxSpeed * (end.WithZ( 0 ) - start.WithZ( 0 )).Normal * 1.25f; 

		var velocity = xyVelocity + Vector3.Up * Math.Max( 500f, (end.z - start.z) * 8f );

		nextGround = 0.5f;
		isGrounded = false;

		Body.Velocity = velocity;

		while ( !FindGround() )
		{
			Agent.Velocity = Body.Velocity;
			await Task.Frame();
		}
	}
	private bool FindGround()
	{
		var tr = Scene.Trace.Ray( WorldPosition + Vector3.Up * 0.1f, WorldPosition + Vector3.Down * 1000 )
			.IgnoreGameObjectHierarchy( GameObject )
			.Run();

		if ( !isGrounded && tr.Hit && nextGround < 0 && tr.Distance < 4f )
		{
			isGrounded = true;
			Model.Set( "b_grounded", true );
			Model.Set( "b_jump", false );
			Agent.SetAgentPosition( tr.EndPosition );
			Agent.CompleteLink( tr.EndPosition );
			return true;
		}

		return false;
	}
}
