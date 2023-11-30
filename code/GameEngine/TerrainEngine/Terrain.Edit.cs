namespace Sandbox.TerrainEngine;

public partial class Terrain
{
	[Range( 1, 128 )]
	[Property] public int BrushRadius { get; set; } = 16;
	[Range( 0, 100.0f )]
	[Property] public float BrushStrength { get; set; } = 10.0f;

	Texture BrushTexture { get; set; } = Texture.Load( FileSystem.Mounted, "Brushes/circle0.png" );
	Color32[] BrushPixels { get; set; }

	string _brush;
	public string Brush
	{
		get => _brush;
		set
		{
			_brush = value;
			BrushTexture = Texture.Load( FileSystem.Mounted, $"Brushes/{_brush}.png" );
			BrushPixels = BrushTexture.GetPixels();
		}
	}

	protected double CalculateFalloff( double Distance, double Radius, double Falloff )
	{
		return Distance < Radius ? 1.0 : Falloff > 0.0 ? Math.Max( 0.0, 1.0 - (Distance - Radius) / Falloff ) : 0.0;
	}

	protected double CalculateSmoothFalloff( double Distance, double Radius, double Falloff )
	{
		var y = CalculateFalloff( Distance, Radius, Falloff );
		return y * y * (3 - 2 * y);
	}

	public void AddHeight( Vector3 pos, bool invert = false )
	{
		// this should really interpolate, but just round to ints for now
		int basex = (int)Math.Round( pos.x / TerrainResolutionInInches );
		int basey = (int)Math.Round( pos.y / TerrainResolutionInInches );

		var x1 = basex - BrushRadius;
		var y1 = basey - BrushRadius;
		var x2 = basex + BrushRadius;
		var y2 = basey + BrushRadius;

		var size = BrushRadius * 2;

		for ( var y = 0; y < size; ++y )
		{
			for ( var x = 0; x < size; ++x )
			{
				var brushWidth = BrushTexture.Width;
				var brushHeight = BrushTexture.Height;

				var brushX = (brushWidth / size) * x;
				var brushY = (brushHeight / size) * y;

				var brushPix = BrushPixels[brushY * brushHeight + brushX];

				//var distance = new Vector2( x - BrushRadius, y - BrushRadius ).Length;
				//var brushValue = CalculateSmoothFalloff( distance, BrushRadius, 0.0f );
				//var value = (int)Math.Round( brushValue * BrushStrength );

				float brushValue = ((float)brushPix.r / 255.0f);
				var value = (int)Math.Round( brushValue * BrushStrength );

				if ( invert ) value = -value;

				var height = TerrainData.GetHeight( x1 + x, y1 + y );
				TerrainData.SetHeight( x1 + x,  y1 + y, (ushort)(height + value) );
			}
		}
	}
}


class BrushPreviewSceneObject : SceneCustomObject
{
	public Texture Texture;
	public float Radius = 16.0f;

	public BrushPreviewSceneObject( SceneWorld world ) : base( world )
	{
		RenderLayer = SceneRenderLayer.Default;
	}

	public override void RenderSceneObject()
	{
		var material = Material.FromShader( "shaders/terrain_brush.shader" );

		VertexBuffer buffer = new();
		buffer.Init( true );

		buffer.AddCube( Vector3.Zero, Vector3.One * Radius * 6 , Rotation.Identity );

		RenderAttributes attributes = new RenderAttributes();
		attributes.Set( "Brush", Texture );
		attributes.Set( "Radius", Radius );

		Graphics.GrabDepthTexture( "DepthBuffer", attributes, false );
		

		buffer.Draw( material, attributes );
	}
}
