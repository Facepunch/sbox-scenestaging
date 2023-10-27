using Sandbox;

public abstract partial class BaseComponent
{
	/// <summary>
	/// A component with this interface can react to interactions with triggers
	/// </summary>
	public interface ITriggerListener
	{
		void OnTriggerEnter( ColliderBaseComponent other );
		void OnTriggerExit( ColliderBaseComponent other );
	}
}
