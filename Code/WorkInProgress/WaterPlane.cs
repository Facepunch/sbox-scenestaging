public sealed class WaterPlane : Component, Component.ExecuteInEditor
{
	[Property] public ModelRenderer WaterRenderer { get; set; }

	[Header( "Dynamic Refraction" )]
	[Property] public bool DynamicRefraction { get; set; }

	[Header( "Dynamic Reflections" )]
	[Property] public bool DynamicReflections { get; set; }
	[Property] public float ReflectionSurfaceOffset { get; set; } = 0;

	private CameraComponent RefractionCamera;
	private Texture _refractTex;

	private CameraComponent _reflectionCam;
	private Texture _reflectionTex;

	[Property] public Texture RefractionTexture => _refractTex;
	[Property] public Texture ReflectionTexture => _reflectionTex;

	protected override void OnUpdate()
	{

	}

	protected override void OnEnabled()
	{
		CreateCamera();

		Tags.Add( "planereflect" );
	}

	private void CreateCamera()
	{
		if ( DynamicRefraction && !RefractionCamera.IsValid() )
		{
			RefractionCamera?.DestroyGameObject();

			var go = new GameObject( GameObject, true, "Refraction Camera" )
			{
				Flags = /*GameObjectFlags.Hidden |*/ GameObjectFlags.Absolute | GameObjectFlags.NotSaved
			};

			_refractTex = Texture.CreateRenderTarget().WithFormat( ImageFormat.RGBA16161616F ).WithSize( Screen.Size * 0.5f ).Create( "_water_refraction" );

			RefractionCamera = go.AddComponent<CameraComponent>( true );
			RefractionCamera.Priority = -100;
			RefractionCamera.IsMainCamera = false;
			RefractionCamera.RenderExcludeTags.Add( "planereflect" );
			RefractionCamera.RenderTarget = _refractTex;
		}

		if ( DynamicReflections && !_reflectionCam.IsValid() )
		{
			_reflectionCam?.DestroyGameObject();

			var go = new GameObject( GameObject, true, "Reflections Camera" )
			{
				Flags = /*GameObjectFlags.Hidden |*/ GameObjectFlags.Absolute | GameObjectFlags.NotSaved
			};

			_reflectionTex = Texture.CreateRenderTarget().WithFormat( ImageFormat.RGBA16161616F ).WithSize( Screen.Size ).Create( "_water_reflection" );

			_reflectionCam = go.AddComponent<CameraComponent>( true );
			_reflectionCam.Priority = -100;
			_reflectionCam.IsMainCamera = false;
			_reflectionCam.RenderExcludeTags.Add( "planereflect" );
			_reflectionCam.RenderTarget = _reflectionTex;
		}

		if ( !DynamicRefraction )
		{
			RefractionCamera?.DestroyGameObject();
			RefractionCamera = default;

			_refractTex?.Dispose();
			_refractTex = default;
		}

		if ( !DynamicReflections )
		{
			_reflectionCam?.DestroyGameObject();
			_reflectionCam = default;

			_reflectionTex?.Dispose();
			_reflectionTex = default;
		}
	}

	void PositionRefractionCamera()
	{
		if ( !DynamicRefraction ) return;
		if ( !RefractionCamera.IsValid() ) return;

		var world = WorldTransform;

		var camera = Scene.Camera;
		var cameraPosition = camera.WorldPosition;
		var cameraRotation = camera.WorldRotation;

		var reflectPlane = new Plane( world.Position, world.Down );
		var viewMatrix = Matrix.CreateWorld( cameraPosition, cameraRotation.Forward, cameraRotation.Up );
		var reflectMatrix = ReflectMatrix( viewMatrix, reflectPlane );

		var reflectionPosition = reflectMatrix.Transform( cameraPosition );
		var reflectionRotation = ReflectRotation( cameraRotation, reflectPlane.Normal );

		RefractionCamera.WorldPosition = camera.WorldPosition;
		RefractionCamera.WorldRotation = camera.WorldRotation;
		RefractionCamera.BackgroundColor = camera.BackgroundColor;
		RefractionCamera.ZNear = camera.ZNear;
		RefractionCamera.ZFar = camera.ZFar;
		RefractionCamera.FieldOfView = camera.FieldOfView;
		RefractionCamera.CustomSize = Screen.Size;
		RefractionCamera.CustomProjectionMatrix = RefractionCamera.CalculateObliqueMatrix( reflectPlane );
		RefractionCamera.RenderExcludeTags.Add( "debugoverlay" );
	}

	void PositionReflectionCamera()
	{
		if ( !DynamicReflections ) return;
		if ( !_reflectionCam.IsValid() ) return;

		var waterTransform = WorldTransform;

		var camera = Scene.Camera;

		var offset = ReflectionSurfaceOffset * waterTransform.Up;
		var reflectPlane = new Plane( waterTransform.Position, waterTransform.Up );

		_reflectionCam.WorldTransform = MirrorTransform( camera.WorldTransform, reflectPlane );
		_reflectionCam.BackgroundColor = camera.BackgroundColor;
		_reflectionCam.ZNear = camera.ZNear;
		_reflectionCam.ZFar = camera.ZFar;
		_reflectionCam.FieldOfView = camera.FieldOfView;
		_reflectionCam.CustomSize = null;// Screen.Size;
		_reflectionCam.RenderExcludeTags.Add( "debugoverlay" );

		reflectPlane = new Plane( waterTransform.Position - offset, waterTransform.Up );
		_reflectionCam.CustomProjectionMatrix = _reflectionCam.CalculateObliqueMatrix( reflectPlane );
		//_reflectionCam.CustomProjectionMatrix = null;
	}

	Transform MirrorTransform( Transform input, Plane plane )
	{
		// Mirror position
		Vector3 mirroredPos = plane.ReflectPoint( input.Position );

		// Reflect forward and up
		Vector3 forward = plane.ReflectDirection( input.Rotation.Forward );
		Vector3 up = plane.ReflectDirection( input.Rotation.Up );

		Rotation mirroredRot = Rotation.LookAt( forward, up );

		return new Transform( mirroredPos, mirroredRot, input.Scale );
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

	protected override void OnPreRender()
	{
		base.OnPreRender();

		CreateCamera();

		if ( !WaterRenderer.IsValid() || !WaterRenderer.SceneObject.IsValid() )
			return;

		WaterRenderer.SceneObject.Attributes.Set( "g_RefractionTexture", _refractTex ?? Texture.White );
		WaterRenderer.SceneObject.Attributes.Set( "g_bRefraction", DynamicRefraction );


		WaterRenderer.SceneObject.Attributes.Set( "g_ReflectionTexture", _reflectionTex ?? Texture.White );
		WaterRenderer.SceneObject.Attributes.Set( "g_bReflection", DynamicReflections );

		//if ( _reflectionTex.IsValid() )
		//{
		//	DebugOverlay.Texture( _reflectionTex, 10, Color.White );
		//}

		PositionRefractionCamera();
		PositionReflectionCamera();
	}
}
