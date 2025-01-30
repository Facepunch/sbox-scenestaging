
namespace Sandbox;

public class Mirror : Component, Component.ExecuteInEditor
{
	private SceneCustomObject PlaneRender;
	private CameraComponent Camera;
	private Texture ReflectionTexture;

	protected override void OnPreRender()
	{
		base.OnPreRender();

		var world = WorldTransform;

		if ( PlaneRender.IsValid() )
			PlaneRender.Transform = world;

		if ( !Camera.IsValid() )
			return;

		var camera = Scene.Camera;
		var cameraPosition = camera.WorldPosition;
		var cameraRotation = camera.WorldRotation;

		var reflectPlane = new Plane( world.Position, world.Up );
		var viewMatrix = Matrix.CreateWorld( cameraPosition, cameraRotation.Forward, cameraRotation.Up );
		var reflectMatrix = ReflectMatrix( viewMatrix, reflectPlane );

		var reflectionPosition = reflectMatrix.Transform( cameraPosition );
		var reflectionRotation = ReflectRotation( cameraRotation, reflectPlane.Normal );

		Camera.WorldPosition = reflectionPosition;
		Camera.WorldRotation = reflectionRotation;
		Camera.BackgroundColor = camera.BackgroundColor;
		Camera.ZNear = camera.ZNear;
		Camera.ZFar = camera.ZFar;
		Camera.FieldOfView = camera.FieldOfView;
		Camera.CustomSize = Screen.Size;
		Camera.CustomProjectionMatrix = Camera.CalculateObliqueMatrix( reflectPlane );
	}

	protected override void OnEnabled()
	{
		base.OnEnabled();

		ReflectionTexture = Texture.CreateRenderTarget( "reflection", ImageFormat.RGBA8888, 1024 );

		CreateCamera();

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

		Camera?.DestroyGameObject();
		Camera = null;

		if ( PlaneRender.IsValid() )
			PlaneRender.Delete();

		PlaneRender = null;
	}

	private void CreateCamera()
	{
		if ( Scene.IsEditor )
			return;

		Camera?.DestroyGameObject();

		var go = new GameObject( GameObject, true, "Reflection Camera" )
		{
			Flags = GameObjectFlags.Hidden | GameObjectFlags.Absolute
		};

		Camera = go.AddComponent<CameraComponent>( true );
		Camera.Priority = -100;
		Camera.IsMainCamera = false;
		Camera.RenderExcludeTags.Add( "Reflection" );
		Camera.RenderTarget = ReflectionTexture;
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

		Graphics.Draw( vertices, 6, Material.Load( "materials/mirror.vmat" ), PlaneRender.Attributes );
	}

	private static Matrix ReflectMatrix( Matrix matrix, Plane plane )
	{
		System.Numerics.Matrix4x4 m = matrix;

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
