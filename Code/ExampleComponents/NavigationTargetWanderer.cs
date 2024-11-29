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

    protected override void OnEnabled()
    {
        _currentTarget = PotentialTargets[Random.Shared.Next(0, PotentialTargets.Count)].WorldPosition;
    }

    protected override void OnFixedUpdate()
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

        NavMeshAgent agent = GetComponent<NavMeshAgent>();

        if (agent == null)
            return;

        agent.MoveTo(_currentTarget);

        if (WorldPosition.WithZ(0).Distance(_currentTarget.WithZ(0)) < 32f)
        {
            _currentTarget = PotentialTargets[Random.Shared.Next(0, PotentialTargets.Count)].WorldPosition;
        }
    }
}
