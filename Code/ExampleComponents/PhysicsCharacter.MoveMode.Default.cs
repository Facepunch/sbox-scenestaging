public sealed partial class PhysicsCharacter : Component
{
	public MoveMode DefaultMoveMode { get; } = new WalkMoveMode();


	public class WalkMoveMode : MoveMode
	{
		public override bool AllowGrounding => true;

		public override int Score( PhysicsCharacter controller ) => 0;

		public override void AddVelocity( PhysicsCharacter controller )
		{
			controller.WishVelocity = controller.WishVelocity.WithZ( 0 );
			base.AddVelocity( controller );
		}
	}
}
