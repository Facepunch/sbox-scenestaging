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
	[Header( "Textures" )]
	[Description( "The octahedral atlas texture (8×3 grid)" )]
	public Texture ColorAtlas { get; set; }

	[Description( "Optional normal map atlas (8×3 grid matching ColorAtlas)" )]
	public Texture NormalAtlas { get; set; }

	/// <summary>
	/// Optional depth atlas for parallax effects.
	/// </summary>
	[Sandbox.ReadOnly]
	public Texture DepthAtlas { get; set; }

	/// <summary>
	/// Bounding box of the object represented by this imposter.
	/// </summary>
	[Header( "Metadata" )]
	[Description( "Bounding box of the object this imposter represents" )]
	public BBox Bounds { get; set; }

	/// <summary>
	/// Pivot offset from bounds center to prefab origin.
	/// This is applied to the sprite position to match the original prefab's pivot point.
	/// </summary>
	[Description( "Pivot offset from bounds center to prefab origin" )]
	public Vector3 PivotOffset { get; set; }

	/// <summary>
	/// Resolution per view (width and height of each of the 8 views in the atlas).
	/// </summary>
	[Description( "Resolution of each individual view in the atlas" )]
	public int ResolutionPerView { get; set; } = 512;

	/// <summary>
	/// UV rectangles for each of the 24 octahedral views.
	/// Layout: 8 columns (horizontal directions), 3 rows (vertical angles).
	/// Row 0: Views from above (-30° pitch)
	/// Row 1: Horizontal views (0° pitch)
	/// Row 2: Views from below (+30° pitch)
	/// Automatically calculated based on 8×3 grid.
	/// </summary>
	[Hide]
	public Rect[] UVRects
	{
		get
		{
			var rects = new Rect[24];
			var uvWidth = 1.0f / 8.0f;   // 8 columns
			var uvHeight = 1.0f / 3.0f;  // 3 rows

			for ( int i = 0; i < 24; i++ )
			{
				int col = i % 8;
				int row = i / 8;
				rects[i] = new Rect( col * uvWidth, row * uvHeight, uvWidth, uvHeight );
			}

			return rects;
		}
	}

	protected override Bitmap CreateAssetTypeIcon( int width, int height )
	{
		return CreateSimpleAssetTypeIcon( "view_in_ar", width, height );
	}
}
