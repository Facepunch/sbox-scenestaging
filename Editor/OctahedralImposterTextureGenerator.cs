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

	protected override async ValueTask<Texture> CreateTexture( Options options, CancellationToken ct )
	{
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

			// Calculate bounds and camera distance
			var bounds = prefabRoot.GetBounds();
			var center = bounds.Center;
			var boundsSize = bounds.Size.Length;
			var cameraDistance = Math.Max( 16f, boundsSize * 0.5f ) * 2f;

			// Setup camera
			using var camera = new SceneCamera( "Imposter Baker" );
			camera.World = scene.SceneWorld;
			camera.FieldOfView = 30f;
			camera.BackgroundColor = Color.Transparent;

			// Setup lighting
			var lightRotation = Rotation.From( 80f, 30f, 0f );
			var lightObject = scene.CreateObject();
			lightObject.Name = "Directional Light";
			lightObject.WorldRotation = lightRotation;
			var light = lightObject.Components.Create<DirectionalLight>();
			light.LightColor = Color.White * 2.5f + Color.Cyan * 0.05f;
			light.SkyColor = Color.White;

			// Tick the scene a couple times to ensure everything is initialized
			scene.GameTick( 0.1f );
			scene.GameTick( 0.1f );

			// 8 horizontal camera directions (0, 45, 90, 135, 180, 225, 270, 315 degrees)
			var cameraRotations = new[]
			{
				Rotation.From( 0f, 0f, 0f ),    // Front
				Rotation.From( 0f, 45f, 0f ),   // Front-Right
				Rotation.From( 0f, 90f, 0f ),   // Right
				Rotation.From( 0f, 135f, 0f ),  // Back-Right
				Rotation.From( 0f, 180f, 0f ),  // Back
				Rotation.From( 0f, 225f, 0f ),  // Back-Left
				Rotation.From( 0f, 270f, 0f ),  // Left
				Rotation.From( 0f, 315f, 0f )   // Front-Left
			};

			// Render each view to a pixmap
			var pixmaps = new Pixmap[8];
			for ( int i = 0; i < 8; i++ )
			{
				var rotation = cameraRotations[i];
				camera.Position = center - rotation.Forward * cameraDistance;
				camera.Rotation = rotation;

				pixmaps[i] = new Pixmap( ResolutionPerView, ResolutionPerView );
				camera.RenderToPixmap( pixmaps[i] );
			}

			// Convert pixmaps to bitmaps
			var bitmaps = new Bitmap[8];
			for ( int i = 0; i < 8; i++ )
			{
				var pngBytes = pixmaps[i].GetPng();
				bitmaps[i] = Bitmap.CreateFromBytes( pngBytes );
			}

			// Create atlas (4x2 grid = 8 views)
			var atlasWidth = ResolutionPerView * 4;
			var atlasHeight = ResolutionPerView * 2;
			var atlas = new Bitmap( atlasWidth, atlasHeight );
			atlas.Clear( Color.Transparent );

			// Composite all views into the atlas
			for ( int i = 0; i < 8; i++ )
			{
				int x = (i % 4) * ResolutionPerView;  // 4 columns
				int y = (i / 4) * ResolutionPerView;  // 2 rows
				var destRect = new Rect( x, y, ResolutionPerView, ResolutionPerView );
				atlas.DrawBitmap( bitmaps[i], destRect );
			}

			// Convert to texture
			return atlas.ToTexture( mips: true );
		}
	}
}
