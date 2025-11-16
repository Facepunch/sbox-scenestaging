using Sandbox;
using Sandbox.Resources;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Editor;

/// <summary>
/// Generates an octahedral imposter texture atlas from a prefab.
/// Renders the prefab from 8 horizontal directions and packs them into a single texture.
/// </summary>
[Title( "Octahedral Imposter" )]
[Icon( "view_in_ar" )]
public class OctahedralImposterTextureGenerator : TextureGenerator
{
	/// <summary>
	/// Path to the prefab to generate the imposter from.
	/// </summary>
	[KeyProperty]
	public string PrefabPath { get; set; }

	/// <summary>
	/// Resolution per view (each of the 8 camera views will be this size).
	/// </summary>
	public int ResolutionPerView { get; set; } = 512;

	/// <summary>
	/// Include normal maps in the output.
	/// </summary>
	public bool IncludeNormals { get; set; } = false;

	/// <summary>
	/// Include depth maps in the output.
	/// </summary>
	public bool IncludeDepth { get; set; } = false;

	/// <summary>
	/// Camera distance multiplier to adjust framing. 1.0 = auto-calculated distance, >1.0 = further away, <1.0 = closer.
	/// </summary>
	public float CameraDistanceMultiplier { get; set; } = 1.0f;

	/// <summary>
	/// Public method to generate the texture (wraps protected CreateTexture for external use).
	/// </summary>
	public async Task<Texture> GenerateTexture()
	{
		var options = new Options();
		return await CreateTexture( options, default );
	}

	protected override async ValueTask<Texture> CreateTexture( Options options, CancellationToken ct )
	{
		// Ensure we're on the main thread for asset loading and scene operations
		await MainThread.Wait();

		if ( string.IsNullOrWhiteSpace( PrefabPath ) )
			return null;

		// Load the prefab
		var prefabAsset = AssetSystem.FindByPath( PrefabPath );
		if ( prefabAsset == null )
		{
			Log.Warning( $"OctahedralImposterTextureGenerator could not find prefab: {PrefabPath}" );
			return null;
		}

		var prefabFile = prefabAsset.LoadResource<PrefabFile>();
		if ( prefabFile == null )
		{
			Log.Error( $"Failed to load prefab: {PrefabPath}" );
			return null;
		}

		// Tell the compiler we're using this file
		if ( options.Compiler is not null )
		{
			options.Compiler.Context.AddCompileReference( PrefabPath );
		}

		// Create a temporary scene for rendering
		var scene = new Scene();
		using ( scene.Push() )
		{
			// Instantiate the prefab
			var prefabScene = SceneUtility.GetPrefabScene( prefabFile );
			var prefabRoot = prefabScene.Clone();

			// Disable any ImposterComponents on the prefab to prevent circular baking
			var imposterComponents = prefabRoot.Components.GetAll<SceneStaging.ImposterComponent>( FindMode.EnabledInSelfAndDescendants );
			foreach ( var imposter in imposterComponents )
			{
				imposter.Enabled = false;
			}

			// Calculate bounds and camera distance
			var bounds = prefabRoot.GetBounds();
			var center = bounds.Center;
			var boundsRadius = bounds.Size.Length * 0.5f; // Half the diagonal

			// Setup camera
			using var camera = new SceneCamera( "Imposter Baker" );
			camera.World = scene.SceneWorld;
			camera.FieldOfView = 30f;
			camera.BackgroundColor = Color.Transparent;
			camera.ZFar = 50000f; // Large far plane to prevent clipping at any distance
			// Don't use debug mode - render with lighting like the preview

			// Calculate distance to fit object perfectly in view
			// Formula: distance = radius / tan(fov/2) * multiplier
			// Add small margin to ensure object isn't clipped
			var fovRadians = camera.FieldOfView * (MathF.PI / 180f);
			var cameraDistance = (boundsRadius / MathF.Tan( fovRadians / 2f )) * 1.02f * CameraDistanceMultiplier;

			// Setup basic lighting
			var lightObject = scene.CreateObject();
			lightObject.Name = "Directional Light";
			var light = lightObject.Components.Create<DirectionalLight>();
			light.LightColor = Color.White;
			light.SkyColor = Color.White;

			// Tick the scene a couple times to ensure everything is initialized
			scene.GameTick( 0.1f );
			scene.GameTick( 0.1f );

			// Generate 24 views: 3 vertical angles × 8 horizontal directions
			// Vertical angles: -30° (top), 0° (middle), +30° (bottom)
			float[] verticalAngles = new[] { -30f, 0f, 30f };
			float[] horizontalAngles = new[] { 0f, 45f, 90f, 135f, 180f, 225f, 270f, 315f };

			int totalViews = verticalAngles.Length * horizontalAngles.Length; // 3 × 8 = 24
			var pixmaps = new Pixmap[totalViews];
			int viewIndex = 0;

			// Arrays for normal maps (if enabled)
			Pixmap[] normalPixmaps = IncludeNormals ? new Pixmap[totalViews] : null;

			// Render each view combination
			for ( int vIdx = 0; vIdx < verticalAngles.Length; vIdx++ )
			{
				float pitch = verticalAngles[vIdx];

				for ( int hIdx = 0; hIdx < horizontalAngles.Length; hIdx++ )
				{
					float yaw = horizontalAngles[hIdx];

					// Create rotation with pitch and yaw
					var rotation = Rotation.From( pitch, yaw, 0f );

					// Position camera at distance, looking at center
					camera.Position = center - rotation.Forward * cameraDistance;
					camera.Rotation = rotation;

					// Render with FullBright mode to get pure albedo
					camera.DebugMode = SceneCameraDebugMode.FullBright;
					pixmaps[viewIndex] = new Pixmap( ResolutionPerView, ResolutionPerView );
					camera.RenderToPixmap( pixmaps[viewIndex] );

					// Render normals if requested
					if ( IncludeNormals )
					{
						camera.DebugMode = SceneCameraDebugMode.NormalMap;
						normalPixmaps[viewIndex] = new Pixmap( ResolutionPerView, ResolutionPerView );
						camera.RenderToPixmap( normalPixmaps[viewIndex] );
					}

					viewIndex++;
				}
			}

			// Convert pixmaps to bitmaps
			var bitmaps = new Bitmap[totalViews];
			for ( int i = 0; i < totalViews; i++ )
			{
				var pngBytes = pixmaps[i].GetPng();
				bitmaps[i] = Bitmap.CreateFromBytes( pngBytes );
			}

			// Create atlas (8×3 grid = 24 views)
			// 8 columns (horizontal directions) × 3 rows (vertical angles)
			var atlasWidth = ResolutionPerView * 8;
			var atlasHeight = ResolutionPerView * 3;
			var atlas = new Bitmap( atlasWidth, atlasHeight );
			atlas.Clear( Color.Transparent );

			// Composite all views into the atlas
			// Layout: Row 0 = top views, Row 1 = middle views, Row 2 = bottom views
			for ( int i = 0; i < totalViews; i++ )
			{
				int col = i % 8;  // 8 horizontal directions per row
				int row = i / 8;  // 3 rows for vertical angles
				int x = col * ResolutionPerView;
				int y = row * ResolutionPerView;
				var destRect = new Rect( x, y, ResolutionPerView, ResolutionPerView );
				atlas.DrawBitmap( bitmaps[i], destRect );
			}

			// Save atlas as PNG next to the source prefab
			var basePath = System.IO.Path.ChangeExtension( prefabAsset.AbsolutePath, null );
			var pngPath = $"{basePath}_atlas.png";
			var oimpPath = $"{basePath}.oimp";

			// Convert bitmap to pixmap for saving
			var atlasPixmap = new Pixmap( atlasWidth, atlasHeight );
			atlasPixmap.UpdateFromPixels( atlas );
			atlasPixmap.SavePng( pngPath );

			Log.Info( $"Saved atlas texture to: {pngPath}" );

			// Generate normal atlas if requested
			Texture normalTexture = null;
			if ( IncludeNormals && normalPixmaps != null )
			{
				// Convert normal pixmaps to bitmaps
				var normalBitmaps = new Bitmap[totalViews];
				for ( int i = 0; i < totalViews; i++ )
				{
					var pngBytes = normalPixmaps[i].GetPng();
					normalBitmaps[i] = Bitmap.CreateFromBytes( pngBytes );
				}

				// Create normal atlas
				var normalAtlas = new Bitmap( atlasWidth, atlasHeight );
				normalAtlas.Clear( new Color( 0.5f, 0.5f, 1.0f ) ); // Default normal: (0, 0, 1) in tangent space

				// Composite normal views into atlas
				for ( int i = 0; i < totalViews; i++ )
				{
					int col = i % 8;
					int row = i / 8;
					int x = col * ResolutionPerView;
					int y = row * ResolutionPerView;
					var destRect = new Rect( x, y, ResolutionPerView, ResolutionPerView );
					normalAtlas.DrawBitmap( normalBitmaps[i], destRect );
				}

				// Save normal atlas
				var normalPngPath = $"{basePath}_normal.png";
				var normalAtlasPixmap = new Pixmap( atlasWidth, atlasHeight );
				normalAtlasPixmap.UpdateFromPixels( normalAtlas );
				normalAtlasPixmap.SavePng( normalPngPath );

				Log.Info( $"Saved normal atlas to: {normalPngPath}" );

				// Register and compile normal texture
				await MainThread.Wait();
				var normalTextureAsset = AssetSystem.RegisterFile( normalPngPath );
				if ( normalTextureAsset != null )
				{
					Log.Info( $"Registered normal texture asset: {normalTextureAsset.Path}" );

					while ( !normalTextureAsset.IsCompiledAndUpToDate )
					{
						await System.Threading.Tasks.Task.Yield();
					}

					Log.Info( $"Normal texture compiled successfully" );
					normalTexture = normalTextureAsset.LoadResource<Texture>();
				}
			}

			// Register the PNG with asset system and wait for compilation
			await MainThread.Wait();
			var textureAsset = AssetSystem.RegisterFile( pngPath );
			Texture texture = null;

			if ( textureAsset != null )
			{
				Log.Info( $"Registered texture asset: {textureAsset.Path}" );

				// Wait for asset compilation (matches ShaderGraph pattern)
				while ( !textureAsset.IsCompiledAndUpToDate )
				{
					await System.Threading.Tasks.Task.Yield();
				}

				Log.Info( $"Texture compiled successfully" );
				texture = textureAsset.LoadResource<Texture>();

				if ( texture == null )
				{
					Log.Warning( $"LoadResource<Texture>() returned null!" );
				}
				else if ( texture.IsError )
				{
					Log.Warning( $"Texture has IsError=true: {texture.ResourceName}" );
				}
				else
				{
					Log.Info( $"Texture loaded successfully: {texture.ResourceName}, Index={texture.Index}" );
				}
			}

			// Create the .oimp asset using AssetSystem
			try
			{
				var asset = AssetSystem.CreateResource( "oimp", oimpPath );
				if ( asset != null )
				{
					// Load as OctahedralImposterAsset and populate
					var imposterAsset = asset.LoadResource<SceneStaging.OctahedralImposterAsset>();
					if ( imposterAsset != null )
					{
						// Set normal atlas if generated (do this FIRST)
						if ( normalTexture != null && !normalTexture.IsError )
						{
							imposterAsset.NormalAtlas = normalTexture;
							Log.Info( $"Set NormalAtlas to compiled texture: {normalTexture.ResourceName}" );
						}

						// Load ColorAtlas texture independently from the textureAsset to avoid texture generator metadata
						// This ensures the .oimp file references a clean texture asset, not one wrapped with generator data
						if ( textureAsset != null )
						{
							var colorTexture = textureAsset.LoadResource<Texture>();
							if ( colorTexture != null && !colorTexture.IsError )
							{
								Log.Info( $"DEBUG ColorAtlas - ResourceName: {colorTexture.ResourceName}, ResourcePath: {colorTexture.ResourcePath}, Index: {colorTexture.Index}" );
								imposterAsset.ColorAtlas = colorTexture;
								Log.Info( $"Set ColorAtlas to compiled texture: {colorTexture.ResourceName}" );
							}
							else
							{
								Log.Warning( $"Cannot set ColorAtlas - colorTexture null={colorTexture == null}, IsError={colorTexture?.IsError ?? false}" );
							}
						}
						else
						{
							Log.Warning( $"Cannot set ColorAtlas - textureAsset is null" );
						}

						imposterAsset.Bounds = bounds;
						imposterAsset.ResolutionPerView = ResolutionPerView;

						// Calculate pivot offset: distance from bounds center to prefab origin
						// Prefab origin is at Vector3.Zero, bounds.Center is the center of the bounding box
						// The offset moves the sprite from center-pivot to match the prefab's original pivot
						imposterAsset.PivotOffset = -bounds.Center;

						// Save the asset to disk
						asset.SaveToDisk( imposterAsset );
						Log.Info( $"Created octahedral imposter asset: {oimpPath}" );
					}
				}
			}
			catch ( Exception ex )
			{
				Log.Warning( $"Failed to create .oimp asset: {ex.Message}" );
			}

			if ( texture != null && !texture.IsError )
			{
				return texture;
			}

			// Fallback: return dynamic texture if file save failed
			return texture;
		}
	}
}
