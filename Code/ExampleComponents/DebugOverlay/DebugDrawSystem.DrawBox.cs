public partial class DebugDrawSystem : GameObjectSystem<DebugDrawSystem>
{

	public void Box( Vector3 position, Vector3 size, Color color = new Color(), float duration = 0, Transform transform = default, bool overlay = false )
		=> Box( BBox.FromPositionAndSize( position, size ), color, duration, transform, overlay );

	public void Box( BBox box, Color color = new Color(), float duration = 0, Transform transform = default, bool overlay = false )
	{
		if ( transform == default ) transform = Transform.Zero;
		if ( color == default ) color = Color.White;

		var c0 = new Vector3( box.Mins.x, box.Mins.y, box.Mins.z );
		var c1 = new Vector3( box.Maxs.x, box.Mins.y, box.Mins.z );
		var c2 = new Vector3( box.Maxs.x, box.Maxs.y, box.Mins.z );
		var c3 = new Vector3( box.Mins.x, box.Maxs.y, box.Mins.z );
		var c4 = new Vector3( box.Mins.x, box.Mins.y, box.Maxs.z );
		var c5 = new Vector3( box.Maxs.x, box.Mins.y, box.Maxs.z );
		var c6 = new Vector3( box.Maxs.x, box.Maxs.y, box.Maxs.z );
		var c7 = new Vector3( box.Mins.x, box.Maxs.y, box.Maxs.z );

		var so = new SceneDynamicObject( Scene.SceneWorld );
		so.Transform = transform;
		so.Material = LineMaterial;
		so.Flags.CastShadows = false;
		so.RenderLayer = overlay ? SceneRenderLayer.OverlayWithoutDepth : SceneRenderLayer.OverlayWithDepth;
		so.Init( Graphics.PrimitiveType.Lines );

		so.AddVertex( new Vertex( c0, color ) );
		so.AddVertex( new Vertex( c1, color ) );
		so.AddVertex( new Vertex( c1, color ) );
		so.AddVertex( new Vertex( c2, color ) );
		so.AddVertex( new Vertex( c2, color ) );
		so.AddVertex( new Vertex( c3, color ) );
		so.AddVertex( new Vertex( c3, color ) );
		so.AddVertex( new Vertex( c0, color ) );
		so.AddVertex( new Vertex( c0, color ) );
		so.AddVertex( new Vertex( c4, color ) );
		so.AddVertex( new Vertex( c1, color ) );
		so.AddVertex( new Vertex( c5, color ) );
		so.AddVertex( new Vertex( c2, color ) );
		so.AddVertex( new Vertex( c6, color ) );
		so.AddVertex( new Vertex( c3, color ) );
		so.AddVertex( new Vertex( c7, color ) );
		so.AddVertex( new Vertex( c4, color ) );
		so.AddVertex( new Vertex( c5, color ) );
		so.AddVertex( new Vertex( c5, color ) );
		so.AddVertex( new Vertex( c6, color ) );
		so.AddVertex( new Vertex( c6, color ) );
		so.AddVertex( new Vertex( c7, color ) );
		so.AddVertex( new Vertex( c7, color ) );
		so.AddVertex( new Vertex( c4, color ) );

		Add( duration, so );
	}
}
