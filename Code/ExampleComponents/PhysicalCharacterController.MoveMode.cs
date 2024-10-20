
public sealed partial class PhysicalCharacterController : Component
{
	public MoveMode CurrentMoveMode { get; private set; }
	public MoveMode DefaultMoveMode { get; } = new WalkMoveMode();

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
	}

	public class WalkMoveMode : MoveMode
	{
		public override bool AllowGrounding => true;

		public override int Score( PhysicalCharacterController controller ) => 0;
	}
}
