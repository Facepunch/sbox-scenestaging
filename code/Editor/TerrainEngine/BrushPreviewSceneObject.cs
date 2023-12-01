namespace Editor.TerrainEngine;

class BrushPreviewSceneObject : SceneCustomObject
{
	public Texture Texture { get; set; }
	public float Radius { get; set; } = 16.0f;
	public Color Color { get; set; } = Color.White;

	public BrushPreviewSceneObject( SceneWorld world ) : base( world )
	{
		RenderLayer = SceneRenderLayer.Default;
	}

	public override void RenderSceneObject()
	{
		var material = Material.FromShader( "shaders/terrain_brush.shader" );

		VertexBuffer buffer = new();
		buffer.Init( true );

		buffer.AddCube( Vector3.Zero, Vector3.One * Radius * 6, Rotation.Identity );

		RenderAttributes attributes = new RenderAttributes();
		attributes.Set( "Brush", Texture );
		attributes.Set( "Radius", Radius );
		attributes.Set( "Color", Color );

		Graphics.GrabDepthTexture( "DepthBuffer", attributes, false );

		buffer.Draw( material, attributes );
	}
}
