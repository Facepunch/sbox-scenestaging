using Sandbox.Volumes;

namespace Sandbox;

[EditorHandle( Icon = "contrast" )]
[Icon( "contrast" )]
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
	public float BlendWeight { get; set; } = 1.0f;

	/// <summary>
	/// Distance from the edge of the volume where blending starts.
	/// 0 means hard edge, higher values create softer transitions.
	/// </summary>
	[Property]
	[Range( 0, 500 )]
	[HideIf( "IsGlobal", true )]
	public float BlendDistance { get; set; } = 50.0f;

	/// <summary>
	/// This is global. Always apply, regardless of the volume bounds.
	/// </summary>
	[Property]
	public bool IsGlobal { get; set; } = false;

	/// <summary>
	/// Preview the post processing when this object is selected in the editor, or when the editor camera is inside the volume.
	/// </summary>
	[Property]
	public bool EditorPreview { get; set; } = true;

	/// <summary>
	/// Override to make infinite if needed
	/// </summary>
	protected override bool IsInfiniteExtents => IsGlobal;

	/// <summary>
	/// Get weight based on position
	/// </summary>
	public float GetWeight( Vector3 pos )
	{
		if ( IsGlobal )
		{
			return BlendWeight;
		}

		var distance = GetEdgeDistance( pos );
		return MathX.Remap( distance, 0, BlendDistance, 0, BlendWeight );
	}
}
