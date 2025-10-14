namespace Sandbox;

/// <summary>
/// A scatter brush represents a reusable preset/layer configuration for the scatter tool.
/// </summary>
[AssetType( Name = "Scatter Brush", Extension = "sbrush", Category = "World", Flags = AssetTypeFlags.NoEmbedding )]
public class ScatterBrush : GameResource
{
	/// <summary>
	/// Display name for this brush preset
	/// </summary>
	[Property]
	public string DisplayName { get; set; } = "New Scatter Brush";

	/// <summary>
	/// Clutter layers that define what objects to scatter
	/// </summary>
	[Property]
	public List<ClutterLayer> Layers { get; set; } = [];

	protected override Bitmap CreateAssetTypeIcon( int width, int height )
	{
		return CreateSimpleAssetTypeIcon( "brush", width, height );
	}
}
