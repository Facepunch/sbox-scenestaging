using Sandbox.Diagnostics;

public class DebugDrawSystem : GameObjectSystem<DebugDrawSystem>
{
	bool inFixedUpdate;

	class Entry : IConfigurableDebug, IDisposable
	{
		public bool CreatedDuringFixed;
		public bool SingleFrame = true;
		public float life;
		public SceneObject sceneObject;

		public void Dispose()
		{
			sceneObject?.Delete();
			sceneObject = default;
		}

		IConfigurableDebug IConfigurableDebug.WithColor( Color color )
		{
			if ( sceneObject is not null )
			{
				sceneObject.Attributes.Set( "g_tint", color );
				sceneObject.ColorTint = color;
			}

			return this;
		}

		IConfigurableDebug IConfigurableDebug.WithTime( float time )
		{
			life = time;
			SingleFrame = false;
			return this;
		}
	}

	List<Entry> entries = new List<Entry>();

	public interface IConfigurableDebug
	{
		public IConfigurableDebug WithColor( Color color )
		{
			return this;
		}

		public IConfigurableDebug WithTime( float time );
	}


	public DebugDrawSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.StartUpdate, -10000, StartUpdate, "BuildDebugOverlays" );
		Listen( Stage.StartFixedUpdate, -10000, StartFixedUpdate, "BuildDebugOverlays" );
		Listen( Stage.FinishFixedUpdate, 10000, EndFixedUpdate, "BuildDebugOverlays" );
	}

	IConfigurableDebug Add( Entry entry )
	{
		entries.Add( entry );
		entry.CreatedDuringFixed = inFixedUpdate;
		return entry;
	}

	public IConfigurableDebug AddLine( Vector3 from, Vector3 to )
	{
		var entry = new Entry();
		entry.life = Time.Delta;

		var so = new SceneDynamicObject( Scene.SceneWorld );
		so.Transform = Transform.Zero;
		so.Material = Material.Load( "materials/gizmo/line.vmat" );
		so.Flags.CastShadows = false;
		so.Init( Graphics.PrimitiveType.Lines );

		so.AddVertex( new Vertex( from, Color32.White ) );
		so.AddVertex( new Vertex( to, Color32.White ) );

		entry.sceneObject = so;

		return Add( entry );
	}

	public IConfigurableDebug AddBox( BBox box ) => AddBox( box, Transform.Zero );

	public IConfigurableDebug AddBox( BBox box, Transform transform )
	{
		return AddBox( new Vector3[8]
{
			new Vector3( box.Mins.x, box.Mins.y, box.Mins.z ),
			new Vector3( box.Maxs.x, box.Mins.y, box.Mins.z ),
			new Vector3( box.Maxs.x, box.Maxs.y, box.Mins.z ),
			new Vector3( box.Mins.x, box.Maxs.y, box.Mins.z ),

			new Vector3( box.Mins.x, box.Mins.y, box.Maxs.z ),
			new Vector3( box.Maxs.x, box.Mins.y, box.Maxs.z ),
			new Vector3( box.Maxs.x, box.Maxs.y, box.Maxs.z ),
			new Vector3( box.Mins.x, box.Maxs.y, box.Maxs.z )
		}, transform );
	}

	private IConfigurableDebug AddBox( Span<Vector3> corners, Transform transform )
	{
		var entry = new Entry();
		entry.life = Time.Delta;

		var so = new SceneDynamicObject( Scene.SceneWorld );
		so.Transform = transform;
		so.Material = Material.Load( "materials/gizmo/line.vmat" );
		so.Flags.CastShadows = false;
		so.Init( Graphics.PrimitiveType.Lines );
		Assert.AreEqual( 8, corners.Length );

		so.AddVertex( new Vertex( corners[0], Color32.White ) );
		so.AddVertex( new Vertex( corners[1], Color32.White ) );
		so.AddVertex( new Vertex( corners[1], Color32.White ) );
		so.AddVertex( new Vertex( corners[2], Color32.White ) );
		so.AddVertex( new Vertex( corners[2], Color32.White ) );
		so.AddVertex( new Vertex( corners[3], Color32.White ) );
		so.AddVertex( new Vertex( corners[3], Color32.White ) );
		so.AddVertex( new Vertex( corners[0], Color32.White ) );


		so.AddVertex( new Vertex( corners[0], Color32.White ) );
		so.AddVertex( new Vertex( corners[4], Color32.White ) );
		so.AddVertex( new Vertex( corners[1], Color32.White ) );
		so.AddVertex( new Vertex( corners[5], Color32.White ) );
		so.AddVertex( new Vertex( corners[2], Color32.White ) );
		so.AddVertex( new Vertex( corners[6], Color32.White ) );
		so.AddVertex( new Vertex( corners[3], Color32.White ) );
		so.AddVertex( new Vertex( corners[7], Color32.White ) );

		so.AddVertex( new Vertex( corners[4], Color32.White ) );
		so.AddVertex( new Vertex( corners[5], Color32.White ) );
		so.AddVertex( new Vertex( corners[5], Color32.White ) );
		so.AddVertex( new Vertex( corners[6], Color32.White ) );
		so.AddVertex( new Vertex( corners[6], Color32.White ) );
		so.AddVertex( new Vertex( corners[7], Color32.White ) );
		so.AddVertex( new Vertex( corners[7], Color32.White ) );
		so.AddVertex( new Vertex( corners[4], Color32.White ) );

		entry.sceneObject = so;

		return Add( entry );
	}

	void RemoveExpired()
	{
		for ( int i = 0; i < entries.Count; i++ )
		{
			if ( entries[i].SingleFrame ) continue;
			entries[i].life -= Time.Delta;

			if ( entries[i].life < 0 )
			{
				entries[i].Dispose();
				entries.RemoveAt( i ); // move to end and remove?
				i--;
			}
		}
	}

	void RemoveSingleFrame( bool createdDuringFixed )
	{
		for ( int i = 0; i < entries.Count; i++ )
		{
			if ( !entries[i].SingleFrame ) continue;
			if ( entries[i].CreatedDuringFixed != createdDuringFixed ) continue;

			entries[i].Dispose();
			entries.RemoveAt( i );
			i--;
		}

		Scene.SceneWorld.DeletePendingObjects();
	}

	void StartUpdate()
	{
		RemoveExpired();
		RemoveSingleFrame( false );
	}

	void StartFixedUpdate()
	{
		RemoveSingleFrame( true );
		inFixedUpdate = true;
	}

	void EndFixedUpdate()
	{
		inFixedUpdate = false;
	}

	internal IConfigurableDebug AddText( Vector3 vector3, string v, float size = 18 )
	{
		var entry = new Entry();
		entry.life = Time.Delta;

		var so = new TextSceneObject( Scene.SceneWorld );
		so.ScreenPosition = Scene.Camera.PointToScreenPixels( vector3 );
		so.Flags.CastShadows = false;
		so.TextBlock = new TextRendering.Scope( v, Color.White, size );

		entry.sceneObject = so;

		return Add( entry );
	}
}

internal class TextSceneObject : SceneCustomObject
{
	public TextRendering.Scope TextBlock;
	public TextFlag TextFlags = TextFlag.Center;
	public Vector2 ScreenPosition;

	public TextSceneObject( SceneWorld sceneWorld ) : base( sceneWorld )
	{
		RenderLayer = SceneRenderLayer.OverlayWithoutDepth;
	}

	public override void RenderSceneObject()
	{
		var pos = ScreenPosition;
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
