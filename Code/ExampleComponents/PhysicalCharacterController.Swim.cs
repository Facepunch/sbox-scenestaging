public sealed partial class PhysicalCharacterController : Component
{
	/// <summary>
	/// Will will update this based on how much you're in a "water" tagged trigger
	/// </summary>
	public float WaterLevel { get; private set; }

	/// <summary>
	/// This is for you to change
	/// </summary>
	public bool IsSwimming { get; set; }
}
