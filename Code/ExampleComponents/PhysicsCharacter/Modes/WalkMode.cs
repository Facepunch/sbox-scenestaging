namespace Sandbox.PhysicsCharacterMode;

/// <summary>
/// The character is walking
/// </summary>
[Icon( "👟" ), Group( "PhysicsCharacterMode" ), Title( "Walk Mode" )]
public partial class PhysicsCharacterWalkMode : BaseMode
{
	[Property]
	public int Priority { get; set; } = 0;

	public override bool AllowGrounding => true;

	public override int Score( PhysicsCharacter controller ) => Priority;

	public override void AddVelocity()
	{
		Controller.WishVelocity = Controller.WishVelocity.WithZ( 0 );
		base.AddVelocity();
	}
}
