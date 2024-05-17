public sealed class JiggleBone : TransformProxyComponent
{
	JiggleBoneState state = new JiggleBoneState();

	[Property]
	public Vector3 StartPoint = new Vector3( 0, 0, 0 );

	[Property]
	public Vector3 EndPoint = new Vector3( 32, 0, 0 );

	[Property, Range( 0, 2 )]
	public float Speed { get; set; } = 1.0f;

	[Property, Range( 0, 2 )]
	public float Stiffness { get; set; } = 1.0f;

	[Property, Range( 0, 2 )]
	public float Damping { get; set; } = 1.0f;

	[Property, Range( 0, 100 )]
	public float Radius { get; set; } = 40.0f;

	[Property, Range( 0, 100 )]
	public float Mass { get; set; } = 1.0f;

	Transform LocalJigglePosition;

	protected override void OnEnabled()
	{
		LocalJigglePosition = Transform.Local;

		base.OnEnabled();

		state = new JiggleBoneState();
	}

	protected override void OnUpdate()
	{
		var oldPos = LocalJigglePosition;



		using ( Transform.DisableProxy() )
		{
			var worldTx = Transform.World;

			var startPoint = worldTx.PointToWorld( StartPoint );
			var endPoint = worldTx.PointToWorld( EndPoint );

			//Gizmo.Draw.LineSphere( startPoint, 1 );
			//Gizmo.Draw.LineSphere( endPoint, 1 );

			state.Extent = (endPoint - startPoint);
			state.Stiffness = Stiffness;
			state.Damping = Damping;
			state.Radius = Radius;
			state.Mass = Mass;

			state.Update( startPoint, Time.Delta * Speed * 16.0f );

			var tx = worldTx.RotateAround( startPoint, state.Rotation );
			LocalJigglePosition = GameObject.Parent.Transform.World.ToLocal( tx );
		}

		if ( oldPos != LocalJigglePosition )
		{
			MarkTransformChanged();
		}
	}

	protected override void DrawGizmos()
	{
		base.DrawGizmos();

		if ( !Gizmo.IsSelected )
			return;

		using ( Transform.DisableProxy() )
		{
			Gizmo.Transform = Transform.World;
			Gizmo.Draw.IgnoreDepth = false;
			Gizmo.Draw.Color = Gizmo.Colors.Yaw.WithAlpha( 0.5f );
			Gizmo.Draw.Line( StartPoint, EndPoint );
			Gizmo.Draw.LineBBox( BBox.FromPositionAndSize( StartPoint, 5 ) );
			Gizmo.Draw.LineBBox( BBox.FromPositionAndSize( EndPoint, 5 ) );
			Gizmo.Draw.LineSphere( EndPoint, Radius * 2.0f, 4 );
		}
	}

	public override Transform GetLocalTransform()
	{
		return LocalJigglePosition;
	}
}

class JiggleBoneState
{
	public Vector3 Extent = new Vector3( 32, 0, 0 );

	public Vector3 Position { get; set; }
	public Rotation Rotation { get; set; }
	public float Stiffness { get; set; } = 1.0f;
	public float Damping { get; set; } = 1.0f;
	public float Radius { get; set; } = 10.0f;
	public float Gravity { get; set; } = 1.0f;
	public float Mass { get; set; } = 1.0f;


	Vector3 basePosition;
	Vector3 velocity;

	public JiggleBoneState()
	{

	}

	internal void Update( Vector3 position, float timeDelta )
	{
		basePosition = position + Extent;

		// initialization
		if ( Position == default )
		{
			Position = basePosition;
		}

		// Calculate spring force based on displacement from the cube
		Vector3 displacement = Position - basePosition;
		Vector3 springForce = -Stiffness * displacement;

		// Calculate acceleration (Newton's second law)
		Vector3 acceleration = springForce / Mass;

		// Update velocity (integrate acceleration)
		velocity += acceleration * timeDelta;

		// Apply exponential damping
		velocity *= (float)Math.Exp( -Damping * timeDelta );

		// Update position (integrate velocity)
		Position += velocity * timeDelta;

		{
			var diff = Position - basePosition;
			var diffLen = diff.Length;
			if ( diffLen > Radius )
			{
				Position = basePosition + diff.Normal * Radius;
				//velocity = velocity.AddClamped( -diff * 2.0f, diff.Length );
			}
		}

		// Store the rotation offset result
		Rotation = Rotation.FromToRotation( basePosition - position, Position - position );

		//Gizmo.Draw.IgnoreDepth = true;
		//Gizmo.Draw.Line( position, Position );
		//Gizmo.Draw.Line( basePosition, Position );
	}
}
