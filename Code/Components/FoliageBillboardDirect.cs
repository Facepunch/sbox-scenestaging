namespace Sandbox;

using Sandbox.Rendering;

/// <summary>
/// Efficiently renders billboard sprites by directly registering with SceneSpriteSystem.
/// Creates ZERO child GameObjects - all sprites managed as a single batch!
/// </summary>
[Title( "Foliage Billboard (Direct Registration)" )]
[Category( "Rendering" )]
[Icon( "forest" )]
public sealed class FoliageBillboardDirect : Component, Component.ExecuteInEditor, ISpriteRenderGroup
{
	/// <summary>
	/// The sprite asset to use for billboards.
	/// </summary>
	[Property, Group( "Appearance" ), MakeDirty]
	public Sprite SpriteAsset { get; set; }

	/// <summary>
	/// Tint color applied to all billboards.
	/// </summary>
	[Property, Group( "Appearance" ), MakeDirty]
	public Color Tint { get; set; } = Color.White;

	/// <summary>
	/// Number of billboard instances.
	/// </summary>
	[Property, Group( "Distribution" ), Range( 1, 10000 ), MakeDirty]
	public int SpriteCount { get; set; } = 50;

	/// <summary>
	/// Radius of distribution sphere.
	/// </summary>
	[Property, Group( "Distribution" ), Range( 1f, 500f ), MakeDirty]
	public float Radius { get; set; } = 50f;

	/// <summary>
	/// Random seed for distribution.
	/// </summary>
	[Property, Group( "Distribution" ), MakeDirty]
	public int Seed { get; set; } = 0;

	/// <summary>
	/// Scale randomization range.
	/// </summary>
	[Property, Group( "Variation" ), MakeDirty]
	public Vector2 ScaleRange { get; set; } = new Vector2( 0.8f, 1.2f );

	/// <summary>
	/// Rotation variation in degrees.
	/// </summary>
	[Property, Group( "Variation" ), Range( 0f, 180f ), MakeDirty]
	public float RotationVariation { get; set; } = 45f;

	/// <summary>
	/// Color variation amount.
	/// </summary>
	[Property, Group( "Variation" ), Range( 0f, 1f ), MakeDirty]
	public float ColorVariation { get; set; } = 0.1f;

	/// <summary>
	/// Base size of billboards.
	/// </summary>
	[Property, Group( "Appearance" ), Range( 1f, 100f ), MakeDirty]
	public float BaseSize { get; set; } = 32f;

	/// <summary>
	/// Whether billboards cast shadows.
	/// </summary>
	[Property, Group( "Rendering" ), MakeDirty]
	public bool CastShadows { get; set; } = true;

	/// <summary>
	/// Use additive blending.
	/// </summary>
	[Property, Group( "Rendering" ), MakeDirty]
	public bool AdditiveBlending { get; set; } = false;

	/// <summary>
	/// Enable lighting.
	/// </summary>
	[Property, Group( "Rendering" ), MakeDirty]
	public bool Lighting { get; set; } = true;

	/// <summary>
	/// Use sphere-projected normals for volumetric shading (creates lush, rounded lighting).
	/// Disable for flat billboard normals (A/B testing).
	/// </summary>
	[Property, Group( "Rendering" ), MakeDirty]
	public bool SphericalNormals { get; set; } = true;

	/// <summary>
	/// Depth feather amount.
	/// </summary>
	[Property, Group( "Rendering" ), Range( 0f, 1f ), MakeDirty]
	public float DepthFeather { get; set; } = 0.1f;

	/// <summary>
	/// Fog strength.
	/// </summary>
	[Property, Group( "Rendering" ), Range( 0f, 1f ), MakeDirty]
	public float FogStrength { get; set; } = 1.0f;

	/// <summary>
	/// Distribution pattern.
	/// </summary>
	[Property, Group( "Distribution" ), MakeDirty]
	public DistributionType Distribution { get; set; } = DistributionType.UniformSphere;

	/// <summary>
	/// Enable automatic LOD based on distance from camera.
	/// </summary>
	[Property, Group( "LOD" ), MakeDirty]
	public bool EnableLOD { get; set; } = false;

	/// <summary>
	/// Distance at which LOD starts reducing density/increasing size.
	/// </summary>
	[Property, Group( "LOD" ), Range( 100f, 2000f ), MakeDirty]
	public float LODStartDistance { get; set; } = 500f;

	/// <summary>
	/// Fade transition range for smooth sprite appearance/disappearance (0 = instant pop, 1 = very gradual).
	/// </summary>
	[Property, Group( "LOD" ), Range( 0f, 1f ), MakeDirty]
	public float LODFadeRange { get; set; } = 0.15f;

	/// <summary>
	/// How aggressively sprites are culled with distance (1 = gentle, 4 = medium, 8 = aggressive).
	/// Higher values remove more sprites at closer distances.
	/// </summary>
	[Property, Group( "LOD" ), Range( 1f, 10f ), MakeDirty]
	public float LODCullingRate { get; set; } = 4f;

	/// <summary>
	/// Minimum distance change (in units) before LOD updates. Higher = better performance, lower = smoother transitions.
	/// </summary>
	[Property, Group( "LOD" ), Range( 10f, 500f ), MakeDirty]
	public float LODUpdateThreshold { get; set; } = 50f;

	/// <summary>
	/// Use fast in-place LOD updates (just modifies alpha) instead of rebuilding sprite array.
	/// Much faster but sprites are always allocated in GPU memory.
	/// </summary>
	[Property, Group( "LOD" ), MakeDirty]
	public bool UseFastLOD { get; set; } = true;

	/// <summary>
	/// Number of sprites to update per frame when using fast LOD.
	/// Lower = spreads work across more frames (smoother), Higher = faster LOD response.
	/// </summary>
	[Property, Group( "LOD" ), Range( 50, 2000 ), MakeDirty]
	public int LODBatchSize { get; set; } = 500;

	/// <summary>
	/// Maximum scale multiplier at far distances (1.0 = no scaling, 2.0 = up to 2x bigger).
	/// Compensates for reduced sprite count by making remaining sprites larger.
	/// </summary>
	[Property, Group( "LOD" ), Range( 1f, 3f ), MakeDirty]
	public float LODMaxScale { get; set; } = 1.5f;

	/// <summary>
	/// Maximum distance from camera to render sprites. Beyond this distance, all sprites are culled. 0 = unlimited.
	/// </summary>
	[Property, Group( "LOD" ), Range( 0f, 10000f ), MakeDirty]
	public float CullDistance { get; set; } = 0f;


	public enum DistributionType
	{
		UniformSphere,
		VolumetricSphere,
		HemisphereDome,
		EquatorDisc
	}

	// ISpriteRenderGroup implementation - defines how sprites are batched
	bool ISpriteRenderGroup.Opaque => true; // Opaque rendering with alpha testing to reduce overdraw
	bool ISpriteRenderGroup.Additive => AdditiveBlending;
	bool ISpriteRenderGroup.Shadows => CastShadows;
	bool ISpriteRenderGroup.IsSorted => false; // No sorting needed for opaque

	private SpriteBatchSceneObject.SpriteData[] _spriteDataArray;
	private bool _isRegistered;
	private int _lastSpriteCount = -1;
	private float _lastRadius = -1;
	private int _lastSeed = -1;
	private DistributionType _lastDistribution;
	private Vector3[] _cachedPositions;
	private Transform _lastTransform;
	private float _lastLODDistance;
	private bool _isUpdating; // Prevents overlapping async updates
	private int _lodBatchIndex; // Track which batch of sprites we're updating
	private float _currentLODFactor; // Cache LOD factor to know when it changes
	private bool _needsFullLODUpdate; // Flag to force full update when LOD changes significantly

	// Cached sprite appearance data to avoid recalculating colors/scales/rotations every update
	private struct SpriteAppearance
	{
		public Color VariedColor;
		public float BaseScale;
		public float Rotation;
		public float LODPriority; // For consistent LOD culling
	}
	private SpriteAppearance[] _cachedAppearances;

	protected override void OnEnabled()
	{
		base.OnEnabled();
		RebuildSprites();
		_lastTransform = Transform.World;
		_lastLODDistance = Scene.Camera != null ? Vector3.DistanceBetween( Transform.World.Position, Scene.Camera.WorldPosition ) : 0f;
	}

	protected override void OnDirty()
	{
		base.OnDirty();
		RebuildSprites();
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();
		UnregisterFromSystem();
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		UnregisterFromSystem();
	}


	[Button( "Rebuild Sprites" )]
	public void RebuildSprites()
	{
		bool needsRebuild = _lastSpriteCount != SpriteCount ||
		                    !_lastRadius.AlmostEqual( Radius, 0.01f ) ||
		                    _lastSeed != Seed ||
		                    _lastDistribution != Distribution;

		if ( needsRebuild )
		{
			GeneratePositions();
		}

		UpdateSpriteData();
	}

	private void GeneratePositions()
	{
		if ( SpriteCount <= 0 || Radius <= 0 )
		{
			_cachedPositions = Array.Empty<Vector3>();
			_cachedAppearances = Array.Empty<SpriteAppearance>();
			return;
		}

		_cachedPositions = new Vector3[SpriteCount];
		_cachedAppearances = new SpriteAppearance[SpriteCount];

		for ( int i = 0; i < SpriteCount; i++ )
		{
			// Position random (for distribution)
			var posRandom = new Random( Seed + i );
			_cachedPositions[i] = GeneratePosition( posRandom );

			// Appearance random (for variation)
			var appearanceRandom = new Random( Seed + 1000 + i );

			// Pre-calculate color variation
			var hsv = Tint.ToHsv();
			var newHue = hsv.Hue + appearanceRandom.Float( -ColorVariation * 30f, ColorVariation * 30f );
			var newSat = hsv.Saturation * appearanceRandom.Float( 1f - ColorVariation * 0.5f, 1f + ColorVariation * 0.5f );
			var newVal = hsv.Value * appearanceRandom.Float( 1f - ColorVariation * 0.3f, 1f + ColorVariation * 0.3f );
			var variedColor = new ColorHsv( newHue, newSat, newVal, Tint.a ).ToColor();

			// Pre-calculate scale and rotation
			float baseScale = appearanceRandom.Float( ScaleRange.x, ScaleRange.y ) * BaseSize;
			float rotation = RotationVariation > 0 ? appearanceRandom.Float( -RotationVariation, RotationVariation ) : 0f;

			// LOD priority for consistent culling - use hash to break sequential correlation
			// This prevents spatial patterns where one quadrant always fades first
			int hash = HashCode.Combine( Seed, i * 2654435761 ); // Large prime to scramble index
			var lodRandom = new Random( hash );
			float lodPriority = lodRandom.Float( 0f, 1f );

			_cachedAppearances[i] = new SpriteAppearance
			{
				VariedColor = variedColor,
				BaseScale = baseScale,
				Rotation = rotation,
				LODPriority = lodPriority
			};
		}

		_lastSpriteCount = SpriteCount;
		_lastRadius = Radius;
		_lastSeed = Seed;
		_lastDistribution = Distribution;
	}

	private void UpdateSpriteData()
	{
		var texture = SpriteAsset?.Animations?.FirstOrDefault()?.Frames?.FirstOrDefault()?.Texture;
		if ( _cachedPositions == null || _cachedPositions.Length == 0 || texture == null )
		{
			UnregisterFromSystem();
			return;
		}

		// Fast LOD mode: Update sprites in-place without rebuilding array
		if ( UseFastLOD && _spriteDataArray != null && _spriteDataArray.Length == _cachedPositions.Length )
		{
			UpdateSpriteLODInPlace();
			RegisterWithSystem();
			return;
		}

		// Full rebuild mode: Create entire sprite array from scratch
		var cameraPos = Scene.Camera?.WorldPosition ?? Vector3.Zero;
		float distanceToComponent = EnableLOD ? Vector3.DistanceBetween( Transform.World.Position, cameraPos ) : 0f;

		// Scale LOD distances by transform scale (bigger foliage = farther LOD cutoff)
		var scale = Transform.World.Scale;
		float avgScale = (scale.x + scale.y) * 0.5f;
		float scaledLODStart = LODStartDistance * avgScale;

		// Calculate LOD factor
		float lodFactor = EnableLOD && distanceToComponent > scaledLODStart
			? (distanceToComponent - scaledLODStart) / scaledLODStart
			: 0f;

		float keepFraction = EnableLOD && lodFactor > 0f
			? 1f / (1f + lodFactor * LODCullingRate)
			: 1f;

		float t = MathF.Min( lodFactor, 2f ) / 2f;
		float scaleMultiplier = EnableLOD ? 1f + t * (LODMaxScale - 1f) : 1f;

		// Build all sprites (for fast LOD mode, we keep all sprites)
		_spriteDataArray = new SpriteBatchSceneObject.SpriteData[_cachedPositions.Length];

		for ( int i = 0; i < _cachedPositions.Length; i++ )
		{
			float fadeFactor = 1f;

			if ( EnableLOD && lodFactor > 0f )
			{
				float spriteLodPriority = _cachedAppearances[i].LODPriority;
				float distanceFromThreshold = spriteLodPriority - keepFraction;

				if ( distanceFromThreshold > LODFadeRange )
				{
					fadeFactor = 0f; // Invisible but still in array
				}
				else if ( distanceFromThreshold > 0f )
				{
					fadeFactor = 1f - (distanceFromThreshold / LODFadeRange);
				}
			}

			_spriteDataArray[i] = CreateSpriteDataThreadSafe(
				_cachedPositions[i],
				_cachedAppearances[i],
				Transform.World,
				texture?.Index ?? Texture.Invalid.Index,
				scaleMultiplier,
				fadeFactor,
				SphericalNormals,
				Lighting,
				FogStrength,
				DepthFeather
			);
		}

		RegisterWithSystem();
	}

	// Fast in-place LOD update - processes sprites in batches across frames
	private void UpdateSpriteLODInPlace()
	{
		if ( _spriteDataArray == null || !EnableLOD ) return;

		var cameraPos = Scene.Camera?.WorldPosition ?? Vector3.Zero;
		float distanceToComponent = Vector3.DistanceBetween( Transform.World.Position, cameraPos );

		// Scale LOD distances by transform scale (bigger foliage = farther LOD cutoff)
		var scale = Transform.World.Scale;
		float avgScale = (scale.x + scale.y) * 0.5f;
		float scaledLODStart = LODStartDistance * avgScale;

		float lodFactor = distanceToComponent > scaledLODStart
			? (distanceToComponent - scaledLODStart) / scaledLODStart
			: 0f;

		if ( lodFactor <= 0f ) return; // No LOD changes needed

		// Check if LOD factor changed significantly - force full update
		float lodDelta = MathF.Abs( lodFactor - _currentLODFactor );
		if ( lodDelta > 0.1f ) // 10% change in LOD factor
		{
			_needsFullLODUpdate = true;
			_currentLODFactor = lodFactor;
			_lodBatchIndex = 0;
		}

		float keepFraction = 1f / (1f + lodFactor * LODCullingRate);
		float t = MathF.Min( lodFactor, 2f ) / 2f;
		float scaleMultiplier = 1f + t * (LODMaxScale - 1f);

		// Determine batch range for this frame
		int batchSize = LODBatchSize;
		int startIndex = _lodBatchIndex;
		int endIndex = Math.Min( startIndex + batchSize, _spriteDataArray.Length );

		// Process batch of sprites
		for ( int i = startIndex; i < endIndex; i++ )
		{
			float spriteLodPriority = _cachedAppearances[i].LODPriority;
			float distanceFromThreshold = spriteLodPriority - keepFraction;

			float fadeFactor = 1f;
			if ( distanceFromThreshold > LODFadeRange )
			{
				fadeFactor = 0f;
			}
			else if ( distanceFromThreshold > 0f )
			{
				fadeFactor = 1f - (distanceFromThreshold / LODFadeRange);
			}

			// Modify color alpha in-place
			var color = _cachedAppearances[i].VariedColor.WithAlpha( _cachedAppearances[i].VariedColor.a * fadeFactor );
			_spriteDataArray[i].TintColor = SpriteBatchSceneObject.SpriteData.PackColor( color );

			// Update scale
			float scalea = _cachedAppearances[i].BaseScale * scaleMultiplier;
			_spriteDataArray[i].Scale = new Vector2( scalea, scalea );
		}

		// Advance batch index for next frame
		_lodBatchIndex = endIndex;

		// If we finished the array, loop back to start
		if ( _lodBatchIndex >= _spriteDataArray.Length )
		{
			_lodBatchIndex = 0;
			_needsFullLODUpdate = false; // Full update complete
		}
	}

	// Thread-safe version of CreateSpriteData
	private SpriteBatchSceneObject.SpriteData CreateSpriteDataThreadSafe(
		Vector3 localPosition,
		SpriteAppearance appearance,
		Transform worldTransform,
		int textureIndex,
		float lodScaleMultiplier,
		float fadeFactor,
		bool sphericalNormals,
		bool lighting,
		float fogStrength,
		float depthFeather )
	{
		var adjustedLocalPos = localPosition / lodScaleMultiplier;
		var worldPos = worldTransform.PointToWorld( adjustedLocalPos );
		var finalColor = appearance.VariedColor.WithAlpha( appearance.VariedColor.a * fadeFactor );
		float scale = appearance.BaseScale * lodScaleMultiplier;
		float rotDegrees = appearance.Rotation;

		int lightingFlag = lighting ? 1 : 0;
		int exponent = lighting ? 128 : 0;
		uint packedExponent = (uint)(((byte)lightingFlag) | (exponent << 16));

		return new SpriteBatchSceneObject.SpriteData
		{
			Position = worldPos,
			Rotation = new Vector3( 0, 0, rotDegrees ),
			Scale = new Vector2( scale, scale ),
			TintColor = SpriteBatchSceneObject.SpriteData.PackColor( finalColor ),
			OverlayColor = SpriteBatchSceneObject.SpriteData.PackColor( Color.Transparent ),
			TextureHandle = textureIndex,
			RenderFlags = 0,
			BillboardMode = 0,
			FogStrength = fogStrength,
			Lighting = packedExponent,
			DepthFeather = depthFeather,
			SamplerIndex = 0,
			Splots = 0,
			Sequence = 0,
			SequenceTime = 0f,
			RotationOffset = -1.0f,
			MotionBlur = Vector4.Zero,
			Velocity = sphericalNormals ? worldTransform.Position : Vector3.Zero,
			BlendSheetUV = Vector4.Zero,
			Offset = Vector2.Zero
		};
	}

	private void RegisterWithSystem()
	{
		var spriteSystem = Scene.GetSystem<SceneSpriteSystem>();
		if ( spriteSystem == null || _spriteDataArray == null ) return;

		// Unregister old if exists
		if ( _isRegistered )
		{
			spriteSystem.UnregisterSpriteBatch( Id );
		}

		// Register new batch - system handles memory management
		spriteSystem.RegisterSpriteBatch(
			Id,
			_spriteDataArray,
			this
		);

		_isRegistered = true;
	}

	private void UnregisterFromSystem()
	{
		if ( !_isRegistered ) return;

		var spriteSystem = Scene?.GetSystem<SceneSpriteSystem>();
		spriteSystem?.UnregisterSpriteBatch( Id );

		_isRegistered = false;
	}

	private Vector3 GeneratePosition( Random random )
	{
		return Distribution switch
		{
			DistributionType.UniformSphere => GenerateUniformSphere( random ),
			DistributionType.VolumetricSphere => GenerateVolumetricSphere( random ),
			DistributionType.HemisphereDome => GenerateHemisphere( random ),
			DistributionType.EquatorDisc => GenerateEquatorDisc( random ),
			_ => Vector3.Zero
		};
	}

	private Vector3 GenerateUniformSphere( Random random )
	{
		float u = random.Float( 0f, 1f );
		float v = random.Float( 0f, 1f );

		float theta = 2f * MathF.PI * u;
		float phi = MathF.Acos( 2f * v - 1f );

		float x = MathF.Sin( phi ) * MathF.Cos( theta );
		float y = MathF.Sin( phi ) * MathF.Sin( theta );
		float z = MathF.Cos( phi );

		return new Vector3( x, y, z ) * Radius;
	}

	private Vector3 GenerateVolumetricSphere( Random random )
	{
		Vector3 point;
		do
		{
			point = new Vector3(
				random.Float( -1f, 1f ),
				random.Float( -1f, 1f ),
				random.Float( -1f, 1f )
			);
		} while ( point.LengthSquared > 1f );

		return point * Radius;
	}

	private Vector3 GenerateHemisphere( Random random )
	{
		Vector3 point = GenerateUniformSphere( random );
		point.z = MathF.Abs( point.z );
		return point;
	}

	private Vector3 GenerateEquatorDisc( Random random )
	{
		float angle = random.Float( 0f, 2f * MathF.PI );
		float discRadius = random.Float( 0f, Radius );
		float height = random.Float( -Radius * 0.2f, Radius * 0.2f );

		return new Vector3(
			MathF.Cos( angle ) * discRadius,
			MathF.Sin( angle ) * discRadius,
			height
		);
	}

	protected override void OnUpdate()
	{
		// Check for transform changes (property changes are handled by OnDirty)
		bool transformChanged = _lastTransform.Position != Transform.World.Position ||
		                        _lastTransform.Rotation != Transform.World.Rotation ||
		                        _lastTransform.Scale != Transform.World.Scale;

		if ( transformChanged )
		{
			RebuildSprites();
			_lastTransform = Transform.World;
		}

		// Check cull distance and unregister if beyond
		if ( CullDistance > 0f && Scene.Camera != null )
		{
			var cameraPos = Scene.Camera.WorldPosition;
			// Calculate 2D distance (XY plane only, ignoring height)
			float currentDistance = Vector2.DistanceBetween(
				new Vector2( Transform.World.Position.x, Transform.World.Position.y ),
				new Vector2( cameraPos.x, cameraPos.y )
			);

			if ( currentDistance > CullDistance )
			{
				// Beyond cull distance - unregister sprites
				if ( _isRegistered )
				{
					UnregisterFromSystem();
				}
				return; // Skip LOD updates
			}
			else if ( !_isRegistered && _spriteDataArray != null )
			{
				// Within cull distance but not registered - re-register
				RegisterWithSystem();
			}
		}

		// Continuously update LOD based on camera distance
		if ( EnableLOD && Scene.Camera != null )
		{
			var cameraPos = Scene.Camera.WorldPosition;
			float currentDistance = Vector3.DistanceBetween( Transform.World.Position, cameraPos );

			// Check if we need to trigger LOD update
			bool distanceChanged = MathF.Abs( currentDistance - _lastLODDistance ) > LODUpdateThreshold;

			if ( UseFastLOD )
			{
				// Fast LOD: Process a batch of sprites every frame
				// This spreads the work across multiple frames
				if ( distanceChanged || _needsFullLODUpdate )
				{
					UpdateSpriteLODInPlace();
					if ( distanceChanged )
						_lastLODDistance = currentDistance;
				}
			}
			else
			{
				// Slow LOD: Full rebuild when distance changes
				if ( distanceChanged )
				{
					UpdateSpriteData();
					_lastLODDistance = currentDistance;
				}
			}
		}
	}

}
