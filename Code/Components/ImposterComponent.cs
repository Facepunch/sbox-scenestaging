using Sandbox;
using Sandbox.Rendering;

namespace SceneStaging;

/// <summary>
/// Runtime component for rendering octahedral imposters using the sprite system.
/// Switches between real prefab and imposter billboard based on distance.
/// </summary>
[Title( "Octahedral Imposter" )]
[Category( "Rendering" )]
[Icon( "view_in_ar" )]
public sealed class ImposterComponent : Component, Component.ExecuteInEditor, ISpriteRenderGroup
{
	/// <summary>
	/// The octahedral imposter asset to use for rendering.
	/// </summary>
	[Property]
	public OctahedralImposterAsset ImposterAsset
	{
		get => _imposterAsset;
		set
		{
			_imposterAsset = value;
			Log.Info( $"ImposterAsset SET to: {value?.GetType().Name ?? "NULL"}" );
		}
	}
	private OctahedralImposterAsset _imposterAsset;

	/// <summary>
	/// Distance at which to switch from real geometry to imposter.
	/// </summary>
	[Property, Range( 0f, 1000f )]
	public float ImposterDistance { get; set; } = 100f;

	/// <summary>
	/// Transition range for smooth LOD blending.
	/// </summary>
	[Property, Range( 0f, 100f )]
	public float TransitionRange { get; set; } = 10f;

	/// <summary>
	/// Size multiplier to adjust the sprite size (1.0 = match original prefab size).
	/// </summary>
	[Property, Range( 0.1f, 5.0f ), Group( "Appearance" )]
	public float SizeMultiplier { get; set; } = 1.0f;

	/// <summary>
	/// Force show imposter for debugging purposes.
	/// </summary>
	[Property, Group( "Debug" )]
	public bool ForceShowImposter { get; set; } = false;

	/// <summary>
	/// Test sprite to verify rendering works.
	/// </summary>
	[Property, Group( "Debug" )]
	public Sprite TestSprite { get; set; }

	/// <summary>
	/// Color tint for the imposter.
	/// </summary>
	[Property, Group( "Appearance" )]
	public Color Tint { get; set; } = Color.White;

	/// <summary>
	/// Enable lighting.
	/// </summary>
	[Property, Group( "Rendering" )]
	public bool Lighting { get; set; } = true;

	// ISpriteRenderGroup implementation
	public bool Opaque => true;
	public bool Additive => false;
	public bool Shadows => false;
	public bool IsSorted => false;

	private SpriteBatchSceneObject.SpriteData[] _spriteDataArray;
	private bool _isRegistered = false;

	protected override void OnEnabled()
	{
		base.OnEnabled();
		Log.Info( $"OnEnabled: ImposterAsset = {ImposterAsset?.GetType().Name ?? "NULL"}" );
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();
		UnregisterImposter();
	}

	protected override void OnStart()
	{
		base.OnStart();
		Log.Info( $"OnStart: ImposterAsset = {ImposterAsset?.GetType().Name ?? "NULL"}" );
		// Register after properties have been deserialized
		RegisterImposter();
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		// Try to register if not yet registered (for editor mode)
		if ( !_isRegistered && ImposterAsset != null && ImposterAsset.ColorAtlas != null )
		{
			RegisterImposter();
		}

		// Update sprite data and re-register (sprite system copies data, so we need to re-register on changes)
		UpdateImposterData();

		// Re-register to upload updated sprite data
		if ( _isRegistered )
		{
			ReregisterWithSystem();
		}
	}

	private void RegisterImposter()
	{
		
		if ( ImposterAsset != null )
		{
		}

		if ( _isRegistered )
		{
			return;
		}

		// Allow registration if either TestSprite or ImposterAsset is set
		bool hasTestSprite = TestSprite != null && TestSprite.Animations?.Count > 0 && TestSprite.Animations[0].Frames?.Count > 0;
		bool hasImposterAsset = ImposterAsset != null && ImposterAsset.ColorAtlas != null;

		if ( !hasTestSprite && !hasImposterAsset )
		{
			Log.Warning( $"ImposterComponent on {GameObject.Name}: Neither TestSprite nor ImposterAsset is set, cannot register" );
			return;
		}

		// Allocate array for one sprite
		_spriteDataArray = new SpriteBatchSceneObject.SpriteData[1];

		// Fill initial data
		UpdateImposterData();

		// Register with sprite system (using same signature as FoliageBillboardDirect)
		var spriteSystem = Scene.GetSystem<SceneSpriteSystem>();
		if ( spriteSystem != null )
		{
			spriteSystem.RegisterSpriteBatch( Id, _spriteDataArray, this );
			_isRegistered = true;
			if ( hasTestSprite )
			{
				var tex = TestSprite.Animations[0].Frames[0].Texture;
			}
			else
			{
				Log.Info( $"  - Using ImposterAsset texture: {ImposterAsset.ColorAtlas.ResourceName}" );
				Log.Info( $"  - Texture Index: {ImposterAsset.ColorAtlas.Index}" );
			}
		}
		else
		{
			Log.Warning( $"ImposterComponent on {GameObject.Name}: SceneSpriteSystem not found in scene" );
		}
	}

	private void UnregisterImposter()
	{
		if ( !_isRegistered )
			return;

		var spriteSystem = Scene.GetSystem<SceneSpriteSystem>();
		if ( spriteSystem != null )
		{
			spriteSystem.UnregisterSpriteBatch( Id );
		}

		_spriteDataArray = null;
		_isRegistered = false;
	}

	private void ReregisterWithSystem()
	{
		var spriteSystem = Scene.GetSystem<SceneSpriteSystem>();
		if ( spriteSystem == null || _spriteDataArray == null ) return;

		// Unregister old
		spriteSystem.UnregisterSpriteBatch( Id );

		// Register with updated data
		spriteSystem.RegisterSpriteBatch( Id, _spriteDataArray, this );
	}

	private void UpdateImposterData()
	{
		if ( !_isRegistered || _spriteDataArray == null || ImposterAsset == null || ImposterAsset.ColorAtlas == null )
			return;

		var bounds = ImposterAsset.Bounds;

		// Use bounding sphere diameter to match the texture generator's framing
		// The generator positions camera to fit the bounding sphere, so sprite should match that size
		float boundsRadius = bounds.Size.Length * 0.5f; // Half the diagonal (bounding sphere)
		float spriteSize = boundsRadius * 2.0f * 1.02f; // Diameter with 2% margin (matches generator)

		// Sprite scale appears to be radius-based rather than diameter, so divide by 2
		spriteSize *= 0.5f;

		// If bounds are zero, use a default size
		if ( spriteSize <= 0.0f )
		{
			spriteSize = 100.0f;
		}

		// Apply user's size multiplier
		spriteSize *= SizeMultiplier;

		// Pack lighting flags (matching FoliageBillboardDirect)
		int lightingFlag = Lighting ? 1 : 0;
		int exponent = Lighting ? 128 : 0;
		uint packedExponent = (uint)(((byte)lightingFlag) | (exponent << 16));

		// Apply pivot offset to match the original prefab's pivot point
		// The pivot offset moves the sprite from center-pivot to the prefab's original pivot
		var spritePosition = GameObject.WorldPosition + ImposterAsset.PivotOffset;

		var spriteData = new SpriteBatchSceneObject.SpriteData
		{
			Position = spritePosition,
			Rotation = new Vector3( 0, 0, 0 ),
			Scale = new Vector2( spriteSize, spriteSize ),
			TintColor = SpriteBatchSceneObject.SpriteData.PackColor( Tint ),
			OverlayColor = SpriteBatchSceneObject.SpriteData.PackColor( Color.Transparent ),
			TextureHandle = ImposterAsset.ColorAtlas.Index,
			RenderFlags = 0x8, // IsImposter flag - enables octahedral UV mapping in shader
			BillboardMode = 0,
			FogStrength = 1.0f,
			Lighting = packedExponent,
			DepthFeather = 0.0f,
			SamplerIndex = 0,
			Splots = 0,
			Sequence = 0,
			SequenceTime = 0f,
			RotationOffset = -1.0f,
			MotionBlur = Vector4.Zero,
			Velocity = Vector3.Zero,
			BlendSheetUV = Vector4.Zero,
			Offset = new Vector2( 0.5f, 0.5f )
		};

		_spriteDataArray[0] = spriteData;
	}

	protected override void DrawGizmos()
	{
		base.DrawGizmos();

		// Test if DrawGizmos is even running - draw at world origin
		//Gizmo.Draw.Color = Color.Cyan;
		//Gizmo.Draw.LineSphere( Vector3.Zero, 100f );
		//
		//
		//if ( ImposterAsset == null )
		//{
		//	Log.Warning( "ImposterAsset is NULL in DrawGizmos!" );
		//	return;
		//}
		//
		//var bounds = ImposterAsset.Bounds;
		//float maxSize = Math.Max( Math.Max( bounds.Size.x, bounds.Size.y ), bounds.Size.z );
		//if ( maxSize <= 0.0f ) maxSize = 100.0f;
		//
		//
		//// Draw spheres using LOCAL position (Gizmo.Transform is already at GameObject position!)
		//Gizmo.Draw.Color = _isRegistered ? Color.Green : Color.Red;
		//
		//// Draw sphere at GameObject position (use Vector3.Zero since we're in local space)
		//Gizmo.Draw.LineSphere( Vector3.Zero, 100f );
		//
		//// Draw spheres along axes
		//Gizmo.Draw.LineSphere( Vector3.Right * 200f, 50f );
		//Gizmo.Draw.LineSphere( Vector3.Left * 200f, 50f );
		//Gizmo.Draw.LineSphere( Vector3.Up * 200f, 50f );
		//Gizmo.Draw.LineSphere( Vector3.Down * 200f, 50f );
	}
}
