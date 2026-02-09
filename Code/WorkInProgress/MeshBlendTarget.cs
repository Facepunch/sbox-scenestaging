namespace Sandbox;

/// <summary>
/// Marks an object for mesh blend seam smoothing.
/// Requires <see cref="MeshBlend"/> post-process on the camera.
/// </summary>
[Title( "Mesh Blend Target" )]
[Category( "Rendering" )]
[Icon( "blur_on" )]
public class MeshBlendTarget : Component
{
	/// <summary>
	/// Depth tolerance for blending. 0 = disabled, each unit ≈ 40" (~1m).
	/// Higher values allow blending across larger depth gaps.
	/// </summary>
	[Property, Range( 0f, 2f )]
	public float BlendFalloff { get; set; } = 1f;

	/// <summary>
	/// Unique region ID derived from component instance hash.
	/// Positive 15-bit range (1-32767) since the mask RT is signed RG16F.
	/// </summary>
	public int RegionId => (Id.GetHashCode() & 0x7FFE) | 1;

	/// <summary>
	/// Returns all renderers in this object and its descendants for the mask pass.
	/// </summary>
	public IEnumerable<Renderer> GetBlendTargets()
	{
		return GameObject.Components.GetAll<Renderer>( FindMode.EnabledInSelfAndDescendants );
	}
}
