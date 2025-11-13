using Sandbox;
using System.ComponentModel;

namespace SceneStaging;

/// <summary>
/// Octahedral imposter asset containing pre-rendered views from 8 octahedral directions.
/// Used for efficient LOD rendering of distant prefabs.
/// </summary>
[AssetType( Name = "Octahedral Imposter", Extension = "oimp", Category = "Rendering" )]
public sealed class OctahedralImposterAsset : GameResource
{
	/// <summary>
	/// Atlas texture containing 8 octahedral views in a 2x4 grid.
	/// </summary>
	[Header( "Textures" )]
	public Texture ColorAtlas { get; set; }

	/// <summary>
	/// Optional normal map atlas for improved lighting.
	/// </summary>
	public Texture NormalAtlas { get; set; }

	/// <summary>
	/// Optional depth atlas for parallax effects.
	/// </summary>
	public Texture DepthAtlas { get; set; }

	/// <summary>
	/// Bounding box of the source object.
	/// </summary>
	[Header( "Metadata" )]
	public BBox Bounds { get; set; }

	/// <summary>
	/// Path to the source prefab that this imposter represents.
	/// </summary>
	public string SourcePrefabPath { get; set; }

	/// <summary>
	/// Total resolution of the atlas texture (e.g., 1024x2048 for 512x512 per view).
	/// </summary>
	public Vector2Int AtlasResolution { get; set; } = new Vector2Int( 1024, 2048 );

	/// <summary>
	/// UV rectangles for each of the 8 octahedral views.
	/// Layout: 2 columns, 4 rows (indices 0-7).
	/// </summary>
	[Hide]
	public Rect[] UVRects { get; set; } = new Rect[8];

	protected override Bitmap CreateAssetTypeIcon( int width, int height )
	{
		return CreateSimpleAssetTypeIcon( "view_in_ar", width, height );
	}
}
