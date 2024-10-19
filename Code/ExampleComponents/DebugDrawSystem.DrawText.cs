public partial class DebugDrawSystem : GameObjectSystem<DebugDrawSystem>
{
	public void Text( Vector3 position, string text, float size = 18, Color color = new Color(), float duration = 0, Transform transform = default )
	{
		if ( transform == default ) transform = Transform.Zero;
		if ( color == default ) color = Color.White;

		var so = new TextSceneObject( Scene.SceneWorld );
		so.Scene = Scene;
		so.WorldPosition = position;
		so.Flags.CastShadows = false;
		so.TextBlock = new TextRendering.Scope( text, color, size );

		Add( new Entry( duration ) { sceneObject = so } );
	}
}



internal class TextSceneObject : SceneCustomObject
{
	public TextRendering.Scope TextBlock;
	public TextFlag TextFlags = TextFlag.Center;
	public Vector3 WorldPosition;
	public Scene Scene;

	public TextSceneObject( SceneWorld sceneWorld ) : base( sceneWorld )
	{
		RenderLayer = SceneRenderLayer.OverlayWithoutDepth;
	}

	public override void RenderSceneObject()
	{
		if ( Scene is null ) return;
		if ( Scene.Camera is null ) return;

		var pos = Scene.Camera.PointToScreenPixels( WorldPosition, out bool behind );
		if ( behind ) return;

		var size = new Vector3( 1024 );

		if ( TextFlags.Contains( TextFlag.CenterHorizontally ) )
		{
			pos.x -= size.x * 0.5f;
		}

		if ( TextFlags.Contains( TextFlag.CenterVertically ) )
		{
			pos.y -= size.y * 0.5f;
		}

		if ( TextFlags.Contains( TextFlag.Bottom ) )
		{
			pos.y -= size.y;
		}

		var rect = new Rect( pos, size );
		Graphics.DrawText( rect, TextBlock, TextFlags );
	}
}
