using Sandbox;


[Title( "Sprite Renderer" )]
[Category( "Rendering" )]
[Icon( "favorite" )]
public sealed class SpriteRenderer : BaseComponent, BaseComponent.ExecuteInEditor
{
	SpriteSceneObject _so;

	[Property] public Texture Texture { get; set; } = Texture.White;
	[Property] public Vector2 Size { get; set; } = 10.0f;
	[Property] public Color Color { get; set; } = Color.White;
	[Property] public bool Additive { get; set; }
	[Property] public bool CastShadows { get; set; }
	[Property] public bool Opaque { get; set; }
	[Property] public float DepthFeather { get; set; }
	[Property] public float FogStrength { get; set; } = 1.0f;


	public override void Update()
	{

	}

	public void Terminate( Particle p )
	{

	}

	public override void OnEnabled()
	{
		_so = new SpriteSceneObject( Scene.SceneWorld );
		_so.Transform = Transform.World;
	}

	public override void OnDisabled()
	{
		_so?.Delete();
		_so = null;
	}

	protected override void OnPreRender()
	{
		_so.Transform = Transform.World;
		_so.Material = Material.FromShader( "code/shaders/sprite.vfx" );
		_so.Flags.CastShadows = CastShadows && !Additive;
		_so.Texture = Texture;

		if ( Additive ) _so.Attributes.SetCombo( "D_BLEND", 1 );
		else _so.Attributes.SetCombo( "D_BLEND", 0 );

		_so.Attributes.SetCombo( "D_OPAQUE", Opaque ? 1 : 0 );

		_so.Attributes.Set( "g_FaceVelocity", 0 );
		_so.Attributes.Set( "g_FaceVelocityOffset", 0 );
		_so.Attributes.Set( "g_DepthFeather", DepthFeather );
		_so.Attributes.Set( "g_FogStrength", FogStrength );
		_so.Attributes.Set( "g_ScreenSize", false );

		_so.Flags.IsOpaque = Opaque;
		_so.Flags.IsTranslucent = !Opaque;

		_so.Bounds = BBox.FromPositionAndSize( _so.Transform.Position, 64 );

		using ( _so.Write( Graphics.PrimitiveType.Points, 1, 0 ) )
		{
			var v = new Vertex();
			var size = Size * Transform.Scale.x;

			v.TexCoord0 = new Vector4( size.x, size.y, Time.Now, 0 );
			v.TexCoord1 = Color;

			v.Position = _so.Transform.Position;

			v.Normal.x = 0;
			v.Normal.y = 0;
			v.Normal.z = 0;

			v.Tangent.x = 0;
			v.Tangent.y = 0;
			v.Tangent.z = 0;

			v.Color.r = (byte)(0 % 255); // sequemce

			_so.AddVertex( v );
		}
	}
}

class SpriteSceneObject : SceneDynamicObject
{
	public Texture Texture
	{
		set
		{
			Attributes.Set( "BaseTexture", value );
			Attributes.Set( "BaseTextureSheet", value.SequenceData );
		}
	}

	public SpriteSceneObject( SceneWorld world ) : base( world )
	{
		RenderLayer = SceneRenderLayer.Default;
	}
}
