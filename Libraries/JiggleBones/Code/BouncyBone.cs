public sealed class BouncyBone : TransformProxyComponent
{
	JiggleBoneState state = new JiggleBoneState();

	[Property]
	public Vector3 Influence { get; set; } = new Vector3( 1, 1, 1 );

	[Property, Range( 0, 50.0f )]
	public float Stiffness { get; set; } = 1;

	[Property, Range( 0, 50.0f )]
	public float Damping { get; set; } = 1;

	Transform LocalJigglePosition;
	TransformSpring springer;

	protected override void OnEnabled()
	{
		springer = new TransformSpring();
		springer.Transform = Transform.World;
		LocalJigglePosition = springer.Transform;

		base.OnEnabled();


	}

	protected override void OnUpdate()
	{
		var oldPos = LocalJigglePosition;

		using ( Transform.DisableProxy() )
		{
			var worldTx = Transform.World;

			springer.Stiffness = Stiffness;
			springer.Damping = Damping;
			springer.UpdateSpring( Transform.World, Time.Delta );

			var tx = GameObject.Parent.Transform.World.ToLocal( springer.Transform );
			LocalJigglePosition = tx;
		}

		if ( oldPos != LocalJigglePosition )
		{
			MarkTransformChanged();
		}
	}

	public override Transform GetLocalTransform()
	{
		return LocalJigglePosition;
	}
}


public struct TransformSpring
{
	public Transform Transform;

	private Vector3 velocityPosition;
	private Vector3 velocityScale;
	private Rotation velocityRotation = Rotation.Identity;

	public float Stiffness = 1.5f;  // Spring stiffness, higher is stiffer
	public float Damping = 1.0f;      // Damping, higher is less oscillation

	public TransformSpring()
	{
		Transform = global::Transform.Zero;
	}

	public void UpdateSpring( Transform target, float deltaTime )
	{
		Transform.Position = SpringLerp( Transform.Position, target.Position, ref velocityPosition, deltaTime );
		Transform.Scale = SpringLerp( Transform.Scale, target.Scale, ref velocityScale, deltaTime );
		Transform.Rotation = target.Rotation;
	}

	private Vector3 SpringLerp( Vector3 current, Vector3 target, ref Vector3 velocity, float deltaTime )
	{
		float omega = 2f * MathF.PI * Stiffness;
		float damper = MathF.Exp( -Damping * deltaTime * omega );

		Vector3 displacement = current - target;
		Vector3 springForce = -omega * omega * displacement;
		Vector3 dampingForce = -2f * omega * Damping * velocity;

		Vector3 acceleration = springForce + dampingForce;
		velocity = (velocity + acceleration * deltaTime) * damper;
		return target + displacement + velocity * deltaTime;
	}


}
