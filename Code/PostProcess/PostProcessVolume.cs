using Sandbox.Volumes;

namespace Sandbox;

public class PostProcessVolume : VolumeComponent, Component.ExecuteInEditor
{
	/// <summary>
	/// Higher priority volumes override lower priority ones. The default priority is 0.
	/// </summary>
	[Property]
	public int Priority { get; set; } = 0;

	/// <summary>
	/// Allows fading in and out
	/// </summary>
	[Property]
	[Range( 0, 1 )]
	public float Weight { get; set; } = 10.0f;

	/// <summary>
	/// Distance from the edge of the volume where blending starts.
	/// 0 means hard edge, higher values create softer transitions.
	/// </summary>
	[Property]
	[Range( 0, 500 )]
	public float BlendDistance { get; set; } = 50.0f;

	/// <summary>
	/// Get weight based on position
	/// </summary>
	public float GetWeight( Vector3 pos )
	{
		var distance = GetEdgeDistance( pos );
		return MathX.Remap( distance, 0, BlendDistance, 0, 1 );
	}
}
