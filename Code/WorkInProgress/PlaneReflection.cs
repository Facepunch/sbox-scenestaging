using Sandbox.Rendering;

public sealed class PlaneReflection : Component, Component.ExecuteInEditor
{
	Relationship<Renderer> _target;

	[Property]
	public Renderer TargetRenderer
	{
		get => _target.Value;
		set => _target.Value = value;
	}

	[Property] public Vector3 PlaneNormal { get; set; } = Vector3.Up;
	[Property] public CameraComponent ReflectionCamera { get; set; }

	/// <summary>
	/// Texture size divider. 0 = screen, 1 = screen/2, 2 = screen/4, 3 = screen/8
	/// </summary>
	[Range( 1, 8 )]
	[Property] public int TextureResolution { get; set; } = 1;

	[Feature( "Debug" )]
	[Property] public bool OverlayTexture { get; set; }

	[FeatureEnabled( "Offsetting" ), Property]
	public bool OffsettingEnabled { get; set; }

	[Property]
	[Feature( "Offsetting" )]
	public float ReflectionSurfaceOffset { get; set; } = 4;

	[FeatureEnabled( "Refraction" ), Property]
	public bool IncludeRefraction { get; set; }

	[Feature( "Refraction" )]
	[ToggleGroup( "RefractionFog" ), Property]
	public bool RefractionFog { get; set; }

	[Feature( "Refraction" )]
	[ToggleGroup( "RefractionFog" ), Property]
	public Color RefractionFogColor { get; set; } = Color.White;

	[Feature( "Refraction" )]
	[ToggleGroup( "RefractionFog" ), Property, Range( -100, 100 )]
	public float RefractionFogHeight { get; set; } = 0;

	[Feature( "Refraction" )]
	[ToggleGroup( "RefractionFog" ), Property, Range( 0, 1000 )]
	public float RefractionFogDepth { get; set; } = 20;

	[Feature( "Refraction" )]
	[ToggleGroup( "RefractionFog" ), Property, Range( 0, 1000 )]
	public float RefractionFogDistance { get; set; } = 100;


	CommandList _drawReflection = new();

	protected override void OnEnabled()
	{
		CreateCamera();

		_target.Init( x => x.ExecuteBefore = _drawReflection, x => x.ExecuteBefore = null );

		//Tags.Add( "planereflect" );
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();

		_target.Shutdown();
	}

	private void CreateCamera()
	{
		if ( ReflectionCamera.IsValid() )
			return;

		var go = new GameObject( GameObject, true, "Reflections Camera" )
		{
			Flags = /*GameObjectFlags.Hidden |*/ GameObjectFlags.Absolute
		};

		ReflectionCamera = go.AddComponent<CameraComponent>( true );
		ReflectionCamera.Priority = -100;
		ReflectionCamera.IsMainCamera = false;
		//	ReflectionCamera.RenderExcludeTags.Add( "planereflect" );
		ReflectionCamera.RenderExcludeTags.Add( "debugoverlay" );
		ReflectionCamera.Enabled = false;
	}



	protected override void OnPreRender()
	{
		base.OnPreRender();

		CreateCamera();

		if ( !TargetRenderer.IsValid() )
			return;

		//
		// Create a command list that runs immediately before the sceneobject is rendered
		//
		_drawReflection.Reset();

		// work out the reflection plane
		var planeNormal = WorldTransform.NormalToWorld( PlaneNormal.Normal );
		var reflectPlane = new Plane( WorldPosition, planeNormal );

		ReflectionCamera.WorldTransform = Scene.Camera.WorldTransform;


		// Refract
		if ( IncludeRefraction )
		{

			var refractionSetup = new RefractionSetup();
			refractionSetup.ClipOffset = ReflectionSurfaceOffset;

			if ( RefractionFog )
			{
				refractionSetup.ViewSetup.GradientFog = new GradientFogSetup
				{
					Enabled = true,
					Color = RefractionFogColor,
					StartDistance = -0.1f,
					EndDistance = RefractionFogDistance,
					StartHeight = WorldPosition.z + RefractionFogHeight - 0.1f - RefractionFogDepth,
					EndHeight = WorldPosition.z + RefractionFogHeight,
					MaximumOpacity = 1,
					DistanceFalloffExponent = 1,
					VerticalFalloffExponent = 1,
				};
			}


			var renderTarget = _drawReflection.GetRenderTarget( "refract", ImageFormat.RGBA16161616F, 1, TextureResolution.Clamp( 1, 8 ) );
			_drawReflection.DrawRefraction( ReflectionCamera, reflectPlane, renderTarget, refractionSetup );
			_drawReflection.Attributes.Set( "HasRefractionTexture", true );
			_drawReflection.Attributes.Set( "RefractionTexture", renderTarget.ColorTexture );

		}

		// Reflect
		if ( true )
		{
			var reflectSetup = new ReflectionSetup();
			reflectSetup.ClipOffset = ReflectionSurfaceOffset;
			reflectSetup.FallbackColor = Color.White * 0.2f;
			reflectSetup.ViewSetup.ZNear = 0.001f;
			reflectSetup.ViewSetup.FlipX = true;
			//reflectSetup.ViewSetup.ClipSpaceBounds = new Vector4( 1, -1, -1, 1 );

			var renderTarget = _drawReflection.GetRenderTarget( "reflect", ImageFormat.RGBA16161616F, 1, TextureResolution.Clamp( 1, 8 ) );

			_drawReflection.DrawReflection( ReflectionCamera, reflectPlane, renderTarget, reflectSetup );

			_drawReflection.Attributes.Set( "HasReflectionTexture", true );
			_drawReflection.Attributes.Set( "ReflectionTexture", renderTarget.ColorTexture );
			_drawReflection.Attributes.Set( "ReflectionColorIndex", renderTarget.ColorIndex );
		}

		TargetRenderer.ExecuteBefore = _drawReflection;
	}
}


public struct Relationship<T>
{
	private T _value;
	Action<T> onStart;
	Action<T> onEnd;

	public T Value
	{
		get => _value;
		set
		{
			if ( EqualityComparer<T>.Default.Equals( _value, value ) )
				return;

			if ( _value != null )
				onEnd?.Invoke( _value );

			_value = value;

			if ( _value != null )
				onStart?.Invoke( _value );
		}
	}

	public void Clear()
	{
		Value = default;
	}

	public Relationship( Action<T> onStart, Action<T> onEnd )
	{
		this.onStart = onStart;
		this.onEnd = onEnd;

		if ( Value is not null )
		{
			this.onStart?.InvokeWithWarning( Value );
		}
	}

	public void Init( Action<T> onStart, Action<T> onEnd )
	{
		this.onStart = onStart;
		this.onEnd = onEnd;

		if ( Value is not null )
		{
			this.onStart?.InvokeWithWarning( Value );
		}
	}

	public void Shutdown()
	{
		if ( Value is not null )
		{
			onEnd?.InvokeWithWarning( Value );
		}
	}
}
