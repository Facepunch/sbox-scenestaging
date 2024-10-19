using System.Buffers;

public partial class DebugDrawSystem : GameObjectSystem<DebugDrawSystem>
{
	public void Sphere( Sphere sphere, Color color = new Color(), float duration = 0, Transform transform = default )
	{
		if ( transform == default ) transform = Transform.Zero;
		if ( color == default ) color = Color.White;

		var so = new SceneDynamicObject( Scene.SceneWorld );
		so.Transform = transform;
		so.Material = LineMaterial;
		so.Flags.CastShadows = false;
		so.Init( Graphics.PrimitiveType.Lines );

		int rings = 8;

		int nOutVert = 0;
		var vertices = ArrayPool<Vector3>.Shared.Rent( rings * rings );

		int i, j;
		for ( i = 0; i < rings; ++i )
		{
			for ( j = 0; j < rings; ++j )
			{
				float u = j / (float)(rings - 1);
				float v = i / (float)(rings - 1);
				float t = 2.0f * MathF.PI * u;
				float p = MathF.PI * v;

				vertices[nOutVert] = new( sphere.Center.x + (sphere.Radius * MathF.Sin( p ) * MathF.Cos( t )), sphere.Center.y + (sphere.Radius * MathF.Sin( p ) * MathF.Sin( t )), sphere.Center.z + (sphere.Radius * MathF.Cos( p )) );
				++nOutVert;
			}
		}

		for ( i = 0; i < rings - 1; ++i )
		{
			for ( j = 0; j < rings - 1; ++j )
			{
				int idx = rings * i + j;

				so.AddVertex( new Vertex( vertices[idx], color ) );
				so.AddVertex( new Vertex( vertices[idx + rings], color ) );

				so.AddVertex( new Vertex( vertices[idx], color ) );
				so.AddVertex( new Vertex( vertices[idx + 1], color ) );
			}
		}


		Add( new Entry( duration ) { sceneObject = so } );
	}
}
