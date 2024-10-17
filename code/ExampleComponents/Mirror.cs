
public class Mirror : Component, Component.ExecuteInEditor
{
	private SceneCustomObject PlaneRender;
	private SceneCamera Camera;
	private Texture ReflectionTexture;

	protected override void OnPreRender()
	{
		base.OnPreRender();

		PlaneRender.Transform = Transform.World;

		var camera = Scene.Camera;
		var cameraPosition = camera.WorldPosition;
		var cameraRotation = camera.WorldRotation;

		var viewMatrix = Matrix.CreateWorld( cameraPosition, cameraRotation.Forward, cameraRotation.Up );
		var reflectMatrix = ReflectMatrix( viewMatrix, new Plane( PlaneRender.Position, PlaneRender.Rotation.Up ) );

		var reflectionPosition = reflectMatrix.Transform( cameraPosition );
		var reflectionRotation = ReflectRotation( cameraRotation, PlaneRender.Rotation.Up );

		Camera.Position = reflectionPosition;
		Camera.Rotation = reflectionRotation;
		Camera.FieldOfView = camera.FieldOfView;
		Camera.BackgroundColor = camera.BackgroundColor;

		Graphics.RenderToTexture( Camera, ReflectionTexture );
	}

	protected override void OnEnabled()
	{
		base.OnEnabled();

		Camera = new SceneCamera( "Reflection" )
		{
			World = Scene.SceneWorld,
			BackgroundColor = Color.Transparent,
			Angles = new Angles( 0, 0, 0 ),
			FieldOfView = 100.0f,
			ZFar = 15000.0f
		};

		Camera.ExcludeTags.Add( "Reflection" );

		ReflectionTexture = Texture.CreateRenderTarget( "reflection", ImageFormat.RGBA8888, new Vector2( 512, 512 ) );

		PlaneRender = new SceneCustomObject( Scene.SceneWorld )
		{
			RenderOverride = Render
		};

		PlaneRender.Tags.Add( "Reflection" );
		PlaneRender.Attributes.Set( "Reflection", ReflectionTexture );
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();

		if ( PlaneRender.IsValid() )
			PlaneRender.Delete();

		PlaneRender = null;
	}

	private void Render( SceneObject sceneObject )
	{
		var size = 64;
		var vertices = new Vertex[]
		{
			new( new Vector3( -size, -size, 0 ), Vector3.Up, Vector3.Forward, new Vector2( -0.5f, -0.5f ) ),
			new( new Vector3( size, -size, 0 ), Vector3.Up, Vector3.Forward, new Vector2( 0.5f, -0.5f ) ),
			new( new Vector3( size, size, 0 ), Vector3.Up, Vector3.Forward, new Vector2( 0.5f, 0.5f ) ),
			new( new Vector3( -size, -size, 0 ), Vector3.Up, Vector3.Forward, new Vector2( -0.5f, -0.5f ) ),
			new( new Vector3( size, size, 0 ), Vector3.Up, Vector3.Forward, new Vector2( 0.5f, 0.5f ) ),
			new( new Vector3( -size, size, 0 ), Vector3.Up, Vector3.Forward, new Vector2( -0.5f, 0.5f ) )
		};

		Graphics.Draw( vertices, 6, Material.Load( "mirror.vmat" ), PlaneRender.Attributes );
	}

	private static Matrix ReflectMatrix( System.Numerics.Matrix4x4 m, Plane plane )
	{
		m.M11 = (1.0f - 2.0f * plane.Normal.x * plane.Normal.x);
		m.M21 = (-2.0f * plane.Normal.x * plane.Normal.y);
		m.M31 = (-2.0f * plane.Normal.x * plane.Normal.z);
		m.M41 = (-2.0f * -plane.Distance * plane.Normal.x);

		m.M12 = (-2.0f * plane.Normal.y * plane.Normal.x);
		m.M22 = (1.0f - 2.0f * plane.Normal.y * plane.Normal.y);
		m.M32 = (-2.0f * plane.Normal.y * plane.Normal.z);
		m.M42 = (-2.0f * -plane.Distance * plane.Normal.y);

		m.M13 = (-2.0f * plane.Normal.z * plane.Normal.x);
		m.M23 = (-2.0f * plane.Normal.z * plane.Normal.y);
		m.M33 = (1.0f - 2.0f * plane.Normal.z * plane.Normal.z);
		m.M43 = (-2.0f * -plane.Distance * plane.Normal.z);

		m.M14 = 0.0f;
		m.M24 = 0.0f;
		m.M34 = 0.0f;
		m.M44 = 1.0f;

		return m;
	}

	private static Rotation ReflectRotation( Rotation source, Vector3 normal )
	{
		return Rotation.LookAt( Vector3.Reflect( source * Vector3.Forward, normal ), Vector3.Reflect( source * Vector3.Up, normal ) );
	}
}
