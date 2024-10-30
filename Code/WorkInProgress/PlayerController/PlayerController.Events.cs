namespace Sandbox;

public sealed partial class PlayerController : Component
{
	/// <summary>
	/// Events from the PlayerController
	/// </summary>
	public interface IEvents : ISceneEvent<IEvents>
	{
		/// <summary>
		/// Our eye angles are changing. Allows you to change the sensitivity, or stomp all together.
		/// </summary>
		void OnEyeAngles( ref Angles angles ) { }

		/// <summary>
		/// Called after we've set the camera up
		/// </summary>
		void PostCameraSetup( CameraComponent cam ) { }

		/// <summary>
		/// The player has landed on the ground, after falling this distance.
		/// </summary>
		void OnLanded( float distance, Vector3 impactVelocity ) { }
	}
}
