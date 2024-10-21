public sealed partial class PhysicsCharacter : Component
{
	/// <summary>
	/// Will will update this based on how much you're in a "water" tagged trigger
	/// </summary>
	public float WaterLevel { get; private set; }

	/// <summary>
	/// True if we're currently using swim mode
	/// </summary>
	public bool IsSwimming => Mode is Sandbox.PhysicsCharacterMode.PhysicsCharacterSwimMode;
}
