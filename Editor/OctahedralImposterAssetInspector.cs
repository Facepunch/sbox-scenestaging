using Editor;
using Sandbox;
using Sandbox.Resources;
using SceneStaging;

namespace Editor;

/// <summary>
/// Custom control widget for OctahedralImposterAsset.
/// The texture generation is handled separately by OctahedralImposterTextureGenerator.
/// </summary>
// Disabled - using default inspector for now
// [CustomEditor( typeof( OctahedralImposterAsset ) )]
public class OctahedralImposterAssetInspector : ResourceControlWidget
{
	public OctahedralImposterAssetInspector( SerializedProperty property ) : base( property )
	{
	}
}
