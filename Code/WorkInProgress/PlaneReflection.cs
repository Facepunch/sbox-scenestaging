public sealed class PlaneReflection : Component, Component.ExecuteInEditor
{
	[Property] public ModelRenderer TargetRenderer { get; set; }
	[Property] public Vector3 PlaneNormal { get; set; } = Vector3.Up;
	[Property] public CameraComponent ReflectionCamera { get; set; }

	/// <summary>
	/// Texture size divider. 0 = screen, 1 = screen/2, 2 = screen/4, 3 = screen/8
	/// </summary>
	[Range( 0, 4 )]
	[Property] public int TextureResolution { get; set; } = 0;

	[Feature( "Debug" )]
	[Property] public bool OverlayTexture { get; set; }

	[FeatureEnabled( "Offsetting" ), Property]
	public bool OffsettingEnabled { get; set; }

	[Property]
	[Feature( "Offsetting" )]
	public float ReflectionSurfaceOffset { get; set; } = 4;

	private Texture _reflectionTex;

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
		if ( ReflectionCamera.IsValid() )
			return;

		var go = new GameObject( GameObject, true, "Reflections Camera" )
		{
			Flags = /*GameObjectFlags.Hidden |*/ GameObjectFlags.Absolute | GameObjectFlags.NotSaved
		};

		ReflectionCamera = go.AddComponent<CameraComponent>( true );
		ReflectionCamera.Priority = -100;
		ReflectionCamera.IsMainCamera = false;
		ReflectionCamera.RenderExcludeTags.Add( "planereflect" );

		CreateTexture();
	}

	private void CreateTexture()
	{
		var divisor = 1 << TextureResolution;

		var texSize = Screen.Size / divisor.Clamp( 1, 32 );
		texSize.x = texSize.x.Clamp( 16, 4096 );
		texSize.y = texSize.y.Clamp( 16, 4096 );

		if ( !_reflectionTex.IsValid() || _reflectionTex.Size != texSize )
		{
			_reflectionTex?.Dispose();
			_reflectionTex = Texture.CreateRenderTarget().WithFormat( ImageFormat.RGBA16161616F ).WithSize( texSize ).Create( "_water_reflection" );
		}

		ReflectionCamera.RenderTarget = _reflectionTex;
	}

	void PositionReflectionCamera()
	{
		if ( !ReflectionCamera.IsValid() ) return;

		var tx = WorldTransform;
		var planeNormal = tx.NormalToWorld( PlaneNormal.Normal );

		var camera = Scene.Camera;


		var reflectPlane = new Plane( tx.Position, planeNormal );

		// we can offset the clip so when the plane intesects with objects, with refraction etc, it doesn't pull pixels
		// in that are inside the object, creating white lines etc. You need to be careful with how much of this is 
		// applied because things will get weird.
		Vector3 offset = 0;
		if ( OffsettingEnabled && !ReflectionSurfaceOffset.AlmostEqual( 0.0f ) )
		{
			offset = ReflectionSurfaceOffset * planeNormal;

			// if we're that close to the water, it's going to be weird, so keep the offset 
			// under that level, always.
			var distance = reflectPlane.GetDistance( camera.WorldPosition );
			if ( distance < ReflectionSurfaceOffset )
			{
				var distanceDelta = distance.Remap( 0.0f, ReflectionSurfaceOffset );
				distanceDelta = MathF.Pow( distanceDelta, 2 );
				offset *= distanceDelta;
			}
		}

		ReflectionCamera.WorldTransform = MirrorTransform( camera.WorldTransform, reflectPlane );
		ReflectionCamera.BackgroundColor = camera.BackgroundColor;
		ReflectionCamera.ZNear = camera.ZNear;
		ReflectionCamera.ZFar = camera.ZFar;
		ReflectionCamera.FieldOfView = camera.FieldOfView;
		ReflectionCamera.CustomSize = null;// Screen.Size;
		ReflectionCamera.RenderExcludeTags.Add( "debugoverlay" );

		reflectPlane = new Plane( tx.Position - offset, tx.Up );
		ReflectionCamera.CustomProjectionMatrix = ReflectionCamera.CalculateObliqueMatrix( reflectPlane );
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

	protected override void OnPreRender()
	{
		base.OnPreRender();

		CreateCamera();
		CreateTexture();

		if ( !TargetRenderer.IsValid() || !TargetRenderer.SceneObject.IsValid() )
			return;

		TargetRenderer.SceneObject.Attributes.Set( "HasReflectionTexture", _reflectionTex.IsValid() );
		TargetRenderer.SceneObject.Attributes.Set( "ReflectionTexture", _reflectionTex ?? Texture.White );
		TargetRenderer.SceneObject.Attributes.Set( "ReflectionNormal", WorldTransform.NormalToWorld( PlaneNormal.Normal ) );

		if ( OverlayTexture && _reflectionTex.IsValid() )
		{
			var rect = new Rect( 20, Screen.Size * 0.5 );


			DebugOverlay.Texture( _reflectionTex, rect, Color.White );
		}

		PositionReflectionCamera();
	}
}
