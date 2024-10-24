public partial class DebugDrawSystem : GameObjectSystem<DebugDrawSystem>
{
	public void Text( Vector3 position, string text, float size = 32, TextFlag flags = TextFlag.Center, Color color = new Color(), float duration = 0, Transform transform = default, bool overlay = false )
	{
		if ( transform == default ) transform = Transform.Zero;
		if ( color == default ) color = Color.White;

		var so = new TextSceneObject( Scene.SceneWorld );
		so.Scene = Scene;
		so.Position = position;
		so.Flags.CastShadows = false;
		so.RenderLayer = overlay ? SceneRenderLayer.OverlayWithoutDepth : SceneRenderLayer.OverlayWithDepth;
		so.TextBlock = new TextRendering.Scope( text, color, size );
		so.LocalBounds = BBox.FromPositionAndSize( 0, 256 );
		so.Pivot = 0.5f;
		so.FontSize = size;

		if ( flags.Contains( TextFlag.Left ) ) so.Pivot.x = 0;
		if ( flags.Contains( TextFlag.Right ) ) so.Pivot.x = 1;

		if ( flags.Contains( TextFlag.Top ) ) so.Pivot.y = 0;
		if ( flags.Contains( TextFlag.Bottom ) ) so.Pivot.y = 1;

		Add( duration, so );
	}
}

internal class TextSceneObject : SceneCustomObject
{
	public TextRendering.Scope TextBlock;
	public TextFlag TextFlags = TextFlag.Center;
	public Scene Scene;
	public Vector2 Pivot;
	public float FontSize;

	Material material;

	public TextSceneObject( SceneWorld sceneWorld ) : base( sceneWorld )
	{
		material = Material.FromShader( "shaders/sprite.shader" );
	}

	public override void RenderSceneObject()
	{
		if ( Scene is null ) return;
		if ( Scene.Camera is null ) return;

		var scale = 0.33f;
		var fontSize = FontSize;
		if ( fontSize < 32 )
		{
			scale *= fontSize.Remap( 0, 32, 0, 1 );
			fontSize = 32;
		}

		var size = new Vector3( 1024 );

		TextBlock.FontSize = fontSize;
		TextBlock.Shadow = new TextRendering.Shadow { Color = Color.Black, Enabled = true, Offset = 2, Size = 4 };

		//var rect = new Rect( pos, size );
		//Graphics.DrawText( rect, TextBlock, TextFlags );

		var texture = TextRendering.GetOrCreateTexture( TextBlock, flag: TextFlags );
		DrawSprite( Position, Rotation, texture.Size * scale, texture );
	}

	void DrawSprite( Vector3 worldPos, Rotation forward, Vector2 scale, Texture texture )
	{
		if ( texture is null )
			return;

		Attributes.SetCombo( "D_OPAQUE", 0 );
		Attributes.SetCombo( "D_ENABLE_LIGHTING", 0 );
		Attributes.Set( "g_Alignment", 0 );
		Attributes.Set( "g_DepthFeather", 0 );
		Attributes.Set( "g_FogStrength", 0 );
		Attributes.Set( "BaseTexture", texture );
		Attributes.Set( "BaseTextureSheet", texture.SequenceData );
		Attributes.Set( "g_ScreenSize", false );
		Attributes.Set( "g_Pivot", Pivot );

		var vertex = new Vertex[1];

		vertex[0].TexCoord0 = new Vector4( scale.x, scale.y, 0, 0 );
		vertex[0].TexCoord1 = ColorTint;
		vertex[0].Position = worldPos;
		vertex[0].Normal.x = 0;
		vertex[0].Normal.y = 0;
		vertex[0].Normal.z = 0;
		vertex[0].Tangent = new Vector4( 0 );
		vertex[0].Color.r = (byte)(0 % 255);

		Graphics.Draw( vertex, 1, default, default, material, Attributes, Graphics.PrimitiveType.Points );
	}
}
