namespace Sandbox.PhysicsCharacterMode;

/// <summary>
/// The character is walking
/// </summary>
[Icon( "👟" ), Group( "PhysicsCharacterMode" ), Title( "Walk Mode" )]
public partial class PhysicsCharacterWalkMode : BaseMode
{
	public override bool AllowGrounding => true;

	public override int Score( PhysicsCharacter controller ) => 0;

	public override void AddVelocity()
	{
		Controller.WishVelocity = Controller.WishVelocity.WithZ( 0 );
		base.AddVelocity();
	}
}
