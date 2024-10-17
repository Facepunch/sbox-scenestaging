namespace Sandbox;

/// <summary>
/// Renders particles as 2D sprites
/// </summary>
[Title( "Particle Line Renderer" )]
[Category( "Particles" )]
[Icon( "favorite" )]
public sealed class ParticleLineRenderer : ParticleRenderer, Component.ExecuteInEditor
{

	SceneLineObject _so;

	[Property] public Texture Texture { get; set; } = Texture.White;
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

	[Group( "Spline" )]
	[Property, Range( 1, 32 )] public int SplineInterpolation { get; set; }

	[Group( "Spline" )]
	[Property, Range( -1, 1 )] public float SplineTension { get; set; }

	[Group( "Spline" )]
	[Property, Range( -1, 1 )] public float SplineContinuity { get; set; }

	[Group( "Spline" )]
	[Property, Range( -1, 1 )] public float SplineBias { get; set; }

	protected override void OnAwake()
	{
		Tags.Add( "particles" );

		base.OnAwake();
	}

	protected override void OnEnabled()
	{
		_so = new SceneLineObject( Scene.SceneWorld );
		_so.Transform = Transform.World;
		_so.Tags.SetFrom( Tags );
	}

	protected override void DrawGizmos()
	{
		if ( _so.IsValid() )
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
		if ( !_so.IsValid() ) return;
		if ( !Components.TryGet( out ParticleEffect effect ) || effect.Particles.Count == 0 )
		{
			_so.RenderingEnabled = false;
			_so.Clear();
			return;
		}

		var viewerPosition = Scene.Camera?.WorldPosition ?? Vector3.Zero;

		_so.RenderingEnabled = true;
		_so.Transform = Transform.World;
		//_so.Material = Material.FromShader( "shaders/sprite.shader" );
		_so.Flags.CastShadows = Shadows && !Additive;
		//	_so.Texture = Texture;

		//	if ( Additive ) _so.Attributes.SetCombo( "D_BLEND", 1 );
		//	else _so.Attributes.SetCombo( "D_BLEND", 0 );

		//	_so.Attributes.SetCombo( "D_OPAQUE", Opaque ? 1 : 0 );

		//	if ( MotionBlur )
		//	{
		//		_so.Attributes.Set( "g_MotionBlur", new Vector4( LeadingTrail ? 2 : 1, BlurAmount.Remap( 0, 1, 0, 6, false ), BlurSpacing.Remap( 0, 1, 0, 1, false ), BlurOpacity ) );
		//	}
		//	else
		//	{
		//		_so.Attributes.Set( "g_MotionBlur", new Vector4( 0, 0, 0, 0 ) );
		//	}

		//	_so.Attributes.Set( "g_FaceVelocity", FaceVelocity );
		//	_so.Attributes.Set( "g_FaceVelocityOffset", RotationOffset );
		//	_so.Attributes.Set( "g_DepthFeather", DepthFeather );
		//	_so.Attributes.Set( "g_FogStrength", FogStrength );


		//	_so.Attributes.Set( "g_ScreenSize", false );

		//	_so.Flags.IsOpaque = Opaque;
		//	_so.Flags.IsTranslucent = !Opaque;

		_so.StartLine();

		{
			var list = effect.Particles;
			var count = list.Count();

			if ( list.Count() == 2 || SplineInterpolation == 1 )
			{
				foreach ( var p in list )
				{
					var size = p.Size * Scale;
					_so.AddLinePoint( p.Position, p.Color.WithAlphaMultiplied( p.Alpha ), size.Length );

				}
			}
			else
			{
				int i = 0;
				int interpolation = SplineInterpolation.Clamp( 1, 100 );
				int totalPoints = (count - 1) * interpolation;
				foreach ( var point in list.Select( x => x.Position ).TcbSpline( interpolation, SplineTension, SplineContinuity, SplineBias ) )
				{
					var p = list[i / interpolation];
					var size = p.Size * Scale;
					_so.AddLinePoint( point, p.Color.WithAlphaMultiplied( p.Alpha ), size.Length );

					i++;
				}
			}

			_so.EndLine();
		}
	}
}
