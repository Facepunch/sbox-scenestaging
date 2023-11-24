using Sandbox;

public abstract partial class BaseComponent
{
	/// <summary>
	/// A component with this interface will draw on the overlay on the camera
	/// </summary>
	public interface IRenderOverlay
	{
		void OnRenderOverlay( SceneCamera camera );
	}
}
