


public sealed partial class PhysicalCharacterController : Component
{
	public MoveMode CurrentMoveMode { get; private set; }

	/// <summary>
	/// A list of move modes. We choose the best move mode by finding the highest ranking .Score 
	/// I kind of feel like these should have been components, since it would all integrate much better that way.
	/// </summary>
	public List<MoveMode> MoveModes { get; } = new List<MoveMode>();

	void ChooseBestMoveMode()
	{
		var mode = MoveModes.Where( x => x.Enabled ).OrderByDescending( x => x.Score( this ) ).First();
		if ( mode == CurrentMoveMode )
			return;

		CurrentMoveMode?.OnDisabled( this );

		CurrentMoveMode = mode;

		CurrentMoveMode?.OnEnabled( this );
	}


	public class MoveMode
	{
		public bool Enabled { get; set; } = true;

		public virtual bool AllowGrounding => false;

		public virtual void OnUpdate( PhysicalCharacterController controller ) { }
		public virtual void OnEnabled( PhysicalCharacterController controller ) { }
		public virtual void OnDisabled( PhysicalCharacterController controller ) { }

		public virtual int Score( PhysicalCharacterController controller ) => 0;

		public virtual void UpdateRigidBody( Rigidbody body )
		{
			body.Gravity = true;
			body.LinearDamping = 0.1f;
			body.AngularDamping = 1f;
		}

		public virtual void AddVelocity( PhysicalCharacterController controller )
		{
			var body = controller.Body;
			var wish = controller.WishVelocity;
			if ( wish.IsNearZeroLength ) return;

			var groundFriction = 0.25f + controller.GroundFriction * 10;
			var groundVelocity = controller.GroundVelocity;

			var z = body.Velocity.z;

			var velocity = (body.Velocity - controller.GroundVelocity);
			var speed = velocity.Length;

			var maxSpeed = MathF.Max( wish.Length, speed );

			if ( controller.IsOnGround )
			{
				var amount = 1 * groundFriction;
				velocity = velocity.AddClamped( wish * amount, wish.Length * amount );
			}
			else
			{
				var amount = 0.05f;
				velocity = velocity.AddClamped( wish * amount, wish.Length );
			}

			if ( velocity.Length > maxSpeed )
				velocity = velocity.Normal * maxSpeed;

			velocity += groundVelocity;

			if ( controller.IsOnGround )
			{
				velocity.z = z;
			}

			body.Velocity = velocity;
		}
	}

}
