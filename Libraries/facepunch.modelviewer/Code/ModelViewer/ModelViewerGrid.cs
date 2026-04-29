using Sandbox;
using Sandbox.UI;

public sealed class ModelViewerGrid : Component, Component.ExecuteInEditor
{
	[Property] Material GridMaterial { get; set; } = Material.Load( "materials/grid.vmat" );
	[Property] Color GridColor { get; set; } = Color.White;
	[Property] Vector2 GridSize { get; set; } = new Vector2( 1, 1 );
	[Property, Range( 0, 2 )] float Roughness { get; set; } = 1f;
	[Property, Range( 0, 1 )] float Metalness { get; set; } = 0.5f;
	protected override void OnUpdate()
	{
		var model = Components.Get<ModelRenderer>();
		model.MaterialOverride = GridMaterial;
		model.SceneObject.Attributes.Set( "TintColor", GridColor );
		model.SceneObject.Attributes.Set( "GridSize", GridSize );
		model.SceneObject.Attributes.Set( "RoughScale", Roughness );
		model.SceneObject.Attributes.Set( "MetalScale", Metalness );
	}
}
