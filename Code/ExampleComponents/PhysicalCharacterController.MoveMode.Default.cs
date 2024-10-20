public sealed partial class PhysicalCharacterController : Component
{
	public MoveMode DefaultMoveMode { get; } = new WalkMoveMode();


	public class WalkMoveMode : MoveMode
	{
		public override bool AllowGrounding => true;

		public override int Score( PhysicalCharacterController controller ) => 0;

		public override void AddVelocity( PhysicalCharacterController controller )
		{
			controller.WishVelocity = controller.WishVelocity.WithZ( 0 );
			base.AddVelocity( controller );
		}
	}
}
