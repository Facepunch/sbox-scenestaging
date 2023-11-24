using Sandbox;

public abstract partial class BaseComponent
{
	/// <summary>
	/// A component with this interface can react to network events
	/// </summary>
	public interface INetworkListener
	{
		/// <summary>
		/// Called when someone connection joins the server
		/// </summary>
		void OnConnected( Connection conn )
		{

		}

		/// <summary>
		/// Called when someone leaves the server
		/// </summary>
		void OnDisconnected( Connection conn )
		{

		}

		/// <summary>
		/// Called when someone is all loaded and entered the game
		/// </summary>
		void OnActive( Connection conn )
		{

		}
	}
}
