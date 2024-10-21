public sealed partial class PhysicsCharacter : Component
{
	/// <summary>
	/// True if we're on a ladder
	/// </summary>
	public bool IsClimbing => Mode is Sandbox.PhysicsCharacterMode.PhysicsCharacterLadderMode;
}
