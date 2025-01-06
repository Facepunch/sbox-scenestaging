using Sandbox;
using System;

public sealed class NavigationTargetWanderer : Component
{
    [Property]
    public List<GameObject> PotentialTargets { get; set; }

    [RequireComponent]
    NavMeshAgent Agent { get; set; }

    [Property]
    public SkinnedModelRenderer Body { get; set; }

    private Vector3 _currentTarget = Vector3.Zero;

    private TimeSince _timeSinceLastTargetChange = 0;

    private TimeSince _timeSinceLastMoveTo = 0;

    protected override void OnEnabled()
    {
        _currentTarget = PotentialTargets[Random.Shared.Next(0, PotentialTargets.Count)].WorldPosition;

        NavMeshAgent agent = GetComponent<NavMeshAgent>();

        if (agent == null)
            return;

        agent.MoveTo(_currentTarget);

		agent.LinkEnter += OnNavLinkEnter;
		agent.LinkExit += OnNavLinkExit;
		agent.LinkUpdate += OnNavLinkUpdate;
	}

	protected override void OnDisabled()
	{
		NavMeshAgent agent = GetComponent<NavMeshAgent>();
		if ( agent == null )
			return;

		agent.LinkEnter -= OnNavLinkEnter;
		agent.LinkExit -= OnNavLinkExit;
		agent.LinkUpdate -= OnNavLinkUpdate;
	}

	private void OnNavLinkEnter()
	{
		Body.Set( "b_grounded", false );
		Body.Set( "b_jump", true );
	}

	private void OnNavLinkExit()
	{
		Body.Set( "b_grounded", true );
	}

	private void OnNavLinkUpdate( float t )
	{

	}

	protected override void OnUpdate()
    {
        var dir = Agent.Velocity;
        var forward = WorldRotation.Forward.Dot(dir);
        var sideward = WorldRotation.Right.Dot(dir);

        var angle = MathF.Atan2(sideward, forward).RadianToDegree().NormalizeDegrees();

        Body.Set("move_direction", angle);
        Body.Set("move_speed", Agent.Velocity.Length);
        Body.Set("move_groundspeed", Agent.Velocity.WithZ(0).Length);
        Body.Set("move_y", sideward);
        Body.Set("move_x", forward);
        Body.Set("move_z", Agent.Velocity.z);

		Body.Set( "wish_x", Agent.WishVelocity.x );
		Body.Set( "wish_y", Agent.WishVelocity.y );
		Body.Set( "wish_z", Agent.WishVelocity.z );

		NavMeshAgent agent = GetComponent<NavMeshAgent>();

        if (agent == null)
            return;

        if (_timeSinceLastTargetChange > 10f || WorldPosition.WithZ(0).Distance(_currentTarget.WithZ(0)) < 32f)
        {
            _currentTarget = PotentialTargets[Random.Shared.Next(0, PotentialTargets.Count)].WorldPosition;
            agent.MoveTo(_currentTarget);
            _timeSinceLastTargetChange = 0;
        }
    }
}
