using System.Numerics;

namespace Sandbox;

public class Mirror : Component, Component.ExecuteInEditor
{
	private SceneCustomObject PlaneRender;
	private CameraComponent Camera;
	private Texture ReflectionTexture;

	[Property, Range( 0.25f, 1f )]
	public float ResolutionScale { get; set; } = 1f;

	protected override void OnPreRender()
	{
		base.OnPreRender();

		if ( Scene.IsEditor )
			return;

		PlaneRender.Transform = Transform.World;

		var camera = Scene.Camera;
		var cameraPosition = camera.WorldPosition;
		var cameraRotation = camera.WorldRotation;

		var targetSize = (camera.ScreenRect.Size * ResolutionScale).SnapToGrid( 4f );

		if ( ReflectionTexture is null || !ReflectionTexture.Size.AlmostEqual( targetSize ) )
		{
			ReflectionTexture?.Dispose();
			ReflectionTexture = Texture.CreateRenderTarget( "reflection", ImageFormat.RGBA8888, targetSize );

			Camera.RenderTarget = ReflectionTexture;
			Camera.GameObject.Enabled = true;

			PlaneRender.Attributes.Set( "Reflection", ReflectionTexture );
		}

		var viewMatrix = Matrix.CreateWorld( cameraPosition, cameraRotation.Forward, cameraRotation.Up );
		var reflectMatrix = ReflectMatrix( viewMatrix, new Plane( WorldPosition, WorldRotation.Up ) );

		var reflectionPosition = reflectMatrix.Transform( cameraPosition );
		var reflectionRotation = ReflectRotation( cameraRotation, WorldRotation.Up );

		Camera.WorldPosition = reflectionPosition;
		Camera.WorldRotation = reflectionRotation;
		Camera.BackgroundColor = camera.BackgroundColor;
		Camera.ZNear = camera.ZNear;
		Camera.ZFar = camera.ZFar;
		Camera.FieldOfView = camera.FieldOfView;

		var projectionMatrix = CreateProjection( Camera );
		var cameraSpaceClipNormal = Camera.WorldRotation.Inverse * WorldRotation.Up;

		// Swizzle so +x is right, +z is forward etc
		cameraSpaceClipNormal = new Vector3(
			cameraSpaceClipNormal.y,
			-cameraSpaceClipNormal.z,
			cameraSpaceClipNormal.x ).Normal;

		projectionMatrix = ModifyProjectionMatrix( projectionMatrix,
			new Vector4( cameraSpaceClipNormal, Vector3.Dot( reflectionPosition - WorldPosition, WorldRotation.Up ) ) );

		Camera.CustomProjectionMatrix = projectionMatrix;
	}

	protected override void OnEnabled()
	{
		base.OnEnabled();

		var cameraObj = new GameObject( false, "Reflection Camera" );

		Camera = cameraObj.AddComponent<CameraComponent>( true );
		Camera.RenderExcludeTags.Add( "Reflection" );

		PlaneRender = new SceneCustomObject( Scene.SceneWorld )
		{
			RenderOverride = Render
		};

		PlaneRender.Tags.Add( "Reflection" );
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

	private static Matrix CreateProjection( CameraComponent camera )
	{
		var tanAngleHorz = MathF.Tan( camera.FieldOfView * 0.5f * MathF.PI / 180f );
		var tanAngleVert = tanAngleHorz * camera.ScreenRect.Height / camera.ScreenRect.Width;

		return CreateProjection( tanAngleHorz, tanAngleVert, camera.ZNear, camera.ZFar );
	}

	private static Matrix ReflectMatrix( Matrix matrix, Plane plane )
	{
		Matrix4x4 m = matrix;

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

	private static float Dot( Vector4 a, Vector4 b )
	{
		return System.Numerics.Vector4.Dot( a, b );
	}

	private static Matrix CreateProjection( float tanAngleHorz, float tanAngleVert, float nearZ, float farZ )
	{
		var invReverseDepth = 1f / (nearZ - farZ);

		var result = new Matrix4x4(
			1f / tanAngleHorz, 0f, 0f, 0f,
			0f, 1f / tanAngleVert, 0f, 0f,
			0f, 0f, farZ * invReverseDepth, farZ * nearZ * invReverseDepth,
			0f, 0f, -1f, 0f
		);

		return result;
	}

	/// <summary>
	/// Pinched from <see href="https://terathon.com/blog/oblique-clipping.html">here</see>
	/// and <see href="https://forum.beyond3d.com/threads/oblique-near-plane-clipping-reversed-depth-buffer.52827/">here</see>.
	/// </summary>
	private static Matrix ModifyProjectionMatrix( Matrix matrix, Vector4 clipPlane )
	{
		Matrix4x4 m = matrix;

		// Calculate the clip-space corner point opposite the clipping plane
		// as (sgn(clipPlane.x), sgn(clipPlane.y), 1, 1) and
		// transform it into camera space by multiplying it
		// by the inverse of the projection matrix

		Vector4 q = default;

		q.x = (MathF.Sign( clipPlane.x ) - m.M13) / m.M11;
		q.y = (MathF.Sign( clipPlane.y ) - m.M23) / m.M22;
		q.z = 1f;
		q.w = (1f - m.M33) / m.M34;

		// Calculate the scaled plane vector
		var c = clipPlane * (1f / Dot( clipPlane, q ));

		// Replace the third row of the projection matrix
		m.M31 = -c.x;
		m.M32 = -c.y;
		m.M33 = -c.z;
		m.M34 = c.w;

		return m;
	}

	private static Rotation ReflectRotation( Rotation source, Vector3 normal )
	{
		return Rotation.LookAt( Vector3.Reflect( source * Vector3.Forward, normal ), Vector3.Reflect( source * Vector3.Up, normal ) );
	}
}
