using System.Buffers;

namespace Sandbox;

/// <summary>
/// Renders particles as 2D sprites
/// </summary>
[Title( "Particle Text Renderer" )]
[Category( "Particles" )]
[Icon( "favorite" )]
public sealed class ParticleTextRenderer : ParticleRenderer, Component.ExecuteInEditor
{
	ParticleSpriteSceneObject _so;
	[Property] public TextRendering.Scope Text { get; set; } = TextRendering.Scope.Default;
	[Property, Range( 0, 50 )] public float DepthFeather { get; set; } = 0.0f;
	[Property, Range( 0, 1 )] public float FogStrength { get; set; } = 1.0f;
	[Property, Range( 0, 2 )] public float Scale { get; set; } = 1.0f;
	[Property] public bool Additive { get; set; }
	[Property] public bool Shadows { get; set; }

	/// <summary>
	/// If opaque there's no need to sort particles, because they will write to
	/// the depth buffer during the opaque pass.
	/// </summary>
	[Property] public bool Opaque { get; set; }

	[Property, ToggleGroup( "FaceVelocity" )]
	public bool FaceVelocity { get; set; }

	[Property, ToggleGroup( "FaceVelocity" )]
	[Range( 0, 360 )] public float RotationOffset { get; set; }

	[Property, ToggleGroup( "MotionBlur" )]
	public bool MotionBlur { get; set; }

	[Property, ToggleGroup( "MotionBlur" )]
	public bool LeadingTrail { get; set; } = true;

	[Property, ToggleGroup( "MotionBlur" ), Range( 0, 1 )]
	public float BlurAmount { get; set; } = 0.5f;

	[Property, ToggleGroup( "MotionBlur" ), Range( 0, 1 )]
	public float BlurSpacing { get; set; } = 0.5f;

	[Property, ToggleGroup( "MotionBlur" ), Range( 0, 1 )]
	public float BlurOpacity { get; set; } = 0.5f;

	public enum BillboardAlignment
	{
		/// <summary>
		/// Look directly at the camera, apply roll
		/// </summary>
		LookAtCamera,

		/// <summary>
		/// Look at the camera but don't pitch up and down, up is always up, can roll
		/// </summary>
		RotateToCamera,

		/// <summary>
		/// Use rotation provided by the particle, pitch yaw and roll
		/// </summary>
		Particle,
	}

	/// <summary>
	/// Should th
	/// </summary>
	[Property]
	public BillboardAlignment Alignment { get; set; } = BillboardAlignment.LookAtCamera;


	public enum ParticleSortMode
	{
		Unsorted,
		ByDistance
	}

	[Property] public ParticleSortMode SortMode { get; set; }

	protected override void OnAwake()
	{
		Tags.Add( "particles" );

		base.OnAwake();
	}

	protected override void OnEnabled()
	{
		_so = new ParticleSpriteSceneObject( this, Scene.SceneWorld );
		_so.Transform = Transform.World;
		_so.Tags.SetFrom( Tags );
	}

	protected override void DrawGizmos()
	{
		if ( _so is not null )
		{
			// Gizmo.Draw.LineBBox( _so.LocalBounds );
		}
	}

	protected override void OnDisabled()
	{
		_so?.Delete();
		_so = null;
	}

	protected override void OnTagsChanged()
	{
		_so?.Tags.SetFrom( Tags );
	}

	protected override void OnPreRender()
	{
		var effect = ParticleEffect;

		if ( _so is null ) return;
		if ( effect is null || effect.Particles.Count == 0 )
		{
			_so.RenderingEnabled = false;
			return;
		}

		var texture = Texture.White;

		_so.RenderingEnabled = true;
		_so.Transform = Transform.World;
		_so.Flags.CastShadows = Shadows && !Additive;
		_so.Bounds = effect.ParticleBounds.Grow( 16.0f + (effect.MaxParticleSize * Scale * 2.0f) ).Snap( 16 );

		if ( Additive ) _so.Attributes.SetCombo( "D_BLEND", 1 );
		else _so.Attributes.SetCombo( "D_BLEND", 0 );

		_so.Attributes.SetCombo( "D_OPAQUE", Opaque ? 1 : 0 );

		if ( MotionBlur )
		{
			_so.Attributes.Set( "g_MotionBlur", new Vector4( LeadingTrail ? 2 : 1, BlurAmount.Remap( 0, 1, 0, 6, false ), BlurSpacing.Remap( 0, 1, 0, 1, false ), BlurOpacity ) );
		}
		else
		{
			_so.Attributes.Set( "g_MotionBlur", new Vector4( 0, 0, 0, 0 ) );
		}

		_so.Attributes.Set( "g_Alignment", (int)Alignment );
		_so.Attributes.Set( "g_FaceVelocity", FaceVelocity );
		_so.Attributes.Set( "g_FaceVelocityOffset", RotationOffset );
		_so.Attributes.Set( "g_DepthFeather", DepthFeather );
		_so.Attributes.Set( "g_FogStrength", FogStrength );

		texture = TextRendering.GetOrCreateTexture( Text, 4096 );

		_so.Attributes.Set( "BaseTexture", texture );
		_so.Attributes.Set( "BaseTextureSheet", texture.SequenceData );
		_so.Attributes.Set( "g_ScreenSize", false );

		_so.Flags.IsOpaque = Opaque;
		_so.Flags.IsTranslucent = !Opaque;
	}
}


internal sealed class ParticleSpriteSceneObject : SceneCustomObject
{
	ParticleTextRenderer owner;
	Material material;

	public ParticleSpriteSceneObject( ParticleTextRenderer owner, SceneWorld world ) : base( world )
	{
		this.owner = owner;

		material = Material.FromShader( "shaders/sprite.shader" );

		//managedNative.ExecuteOnMainThread = false;
	}

	/// <summary>
	/// WARNING - this is running in a thread!
	/// </summary>
	public override void RenderSceneObject()
	{
		var effect = owner.ParticleEffect;

		if ( effect is null || effect.Particles.Count == 0 )
			return;

		var list = effect.Particles.AsEnumerable();
		var viewerPosition = Graphics.CameraPosition;

		if ( !owner.Opaque && owner.SortMode == ParticleTextRenderer.ParticleSortMode.ByDistance )
		{
			list = list.OrderByDescending( x => x.Position.DistanceSquared( viewerPosition ) );
		}

		int count = effect.Particles.Count;
		var vertex = ArrayPool<Vertex>.Shared.Rent( count );

		// in the old c++ source engine one they do

		// CDynamicVertexData<VERTEXCLASS> vb( opCtx.m_pRenderContext, nToDo, "particles", "particles" );


		var t = Attributes.GetTexture( "BaseTexture" );
		if ( t is null )
			return;

		var aspect = t.Size.x / t.Size.y;


		int i = 0;
		foreach ( var p in list )
		{
			var size = p.Size * owner.Scale * 10.0f;

			float sequenceTime = p.SequenceTime.y + p.SequenceTime.z;

			// x is a sequence delta, need to multiply by the sequence length
			if ( p.SequenceTime.x > 0 )
			{
				sequenceTime += p.SequenceTime.x;
			}

			vertex[i].TexCoord0 = new Vector4( size.x * aspect, size.y, sequenceTime, 0 );
			vertex[i].TexCoord1 = p.Color;
			vertex[i].TexCoord1.w *= p.Alpha;

			vertex[i].Position = p.Position;

			vertex[i].Normal.x = p.Angles.pitch;
			vertex[i].Normal.y = p.Angles.yaw;
			vertex[i].Normal.z = p.Angles.roll;

			vertex[i].Tangent = new Vector4( p.Velocity, 0 );

			vertex[i].Color.r = (byte)(p.Sequence % 255);
			i++;
		}

		Graphics.Draw( vertex, count, material, Attributes, primitiveType: Graphics.PrimitiveType.Points );

		ArrayPool<Vertex>.Shared.Return( vertex );
	}
}
