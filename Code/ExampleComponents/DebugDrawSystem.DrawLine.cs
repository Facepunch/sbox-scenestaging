public partial class DebugDrawSystem : GameObjectSystem<DebugDrawSystem>
{
	readonly Material LineMaterial;

	public void Line( Line line, Color color = new Color(), float duration = 0 )
	{
		Line( line.Start, line.End, color, duration );
	}

	public void Line( Vector3 from, Vector3 to, Color color = new Color(), float duration = 0, Transform transform = default )
	{
		if ( transform == default ) transform = Transform.Zero;
		if ( color == default ) color = Color.White;

		var so = new SceneDynamicObject( Scene.SceneWorld );
		so.Transform = transform;
		so.Material = LineMaterial;
		so.Flags.CastShadows = false;
		so.Init( Graphics.PrimitiveType.Lines );

		so.AddVertex( new Vertex( from, color ) );
		so.AddVertex( new Vertex( to, color ) );

		Add( new Entry( duration ) { sceneObject = so } );
	}

	public void Line( IEnumerable<Vector3> points, Color color = new Color(), float duration = 0, Transform transform = default )
	{
		if ( transform == default ) transform = Transform.Zero;
		if ( color == default ) color = Color.White;


		var so = new SceneDynamicObject( Scene.SceneWorld );
		so.Transform = Transform.Zero;
		so.Material = LineMaterial;
		so.Flags.CastShadows = false;
		so.Init( Graphics.PrimitiveType.LineStrip );

		foreach ( var p in points )
		{
			so.AddVertex( new Vertex( p, color ) );
		}

		Add( new Entry( duration ) { sceneObject = so } );
	}
}
