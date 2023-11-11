using Sandbox;
using Sandbox.Utility;
using System.Collections.Generic;

public sealed class ParticleSpriteRenderer : BaseComponent, BaseComponent.ExecuteInEditor
{
	SpriteSceneObject _so;
	[Property] public Texture Texture { get; set; } = Texture.White;
	[Property, Range( 0, 2 )] public float Scale { get; set; } = 1.0f;
	[Property] public bool Additive { get; set; }
	[Property] public bool Shadows { get; set; }

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


	public enum ParticleSortMode
	{
		Unsorted,
		ByDistance
	}

	[Property] public ParticleSortMode SortMode { get; set; }

	public override void OnEnabled()
	{
		_so = new SpriteSceneObject( Scene.SceneWorld );
		_so.Transform = Transform.World;
	}

	public override void DrawGizmos()
	{
		if ( _so is not null )
		{
			// Gizmo.Draw.LineBBox( _so.LocalBounds );
		}
	}

	public override void OnDisabled()
	{
		_so?.Delete();
		_so = null;
	}

	protected override void OnPreRender()
	{
		if ( _so is null ) return;
		if ( !TryGetComponent( out ParticleEffect effect ) ) return;

		_so.Transform = Transform.World;
		_so.Material = Material.FromShader( "code/shaders/sprite.vfx" );
		_so.Flags.CastShadows = Shadows && !Additive;
		_so.Texture = Texture;

		if ( Additive ) _so.Attributes.SetCombo( "D_BLEND", 1 );
		else _so.Attributes.SetCombo( "D_BLEND", 0 );

		if ( MotionBlur )
		{
			_so.Attributes.Set( "g_MotionBlur", new Vector4( LeadingTrail ? 2 : 1, BlurAmount.Remap( 0, 1, 0, 6, false ), BlurSpacing.Remap( 0, 1, 0, 1, false ), BlurOpacity ) );
		}
		else
		{
			_so.Attributes.Set( "g_MotionBlur", new Vector4( 0, 0, 0, 0 ) );
		}

		_so.Attributes.Set( "g_FaceVelocity", FaceVelocity );
		_so.Attributes.Set( "g_FaceVelocityOffset", RotationOffset );


		_so.Attributes.Set( "g_ScreenSize", false );


		BBox bounds = BBox.FromPositionAndSize( _so.Transform.Position, 10 );

		using ( _so.Write( Graphics.PrimitiveType.Points, effect.Particles.Count, 0 ) )
		{
			var list = effect.Particles.AsEnumerable();

			if ( SortMode == ParticleSortMode.ByDistance )
			{
				list = list.OrderByDescending( x => x.Position.DistanceSquared( Camera.Position ) );
			}

			foreach ( var p in list )
			{
				bounds = bounds.AddPoint( p.Position );

				var v = new Vertex();
				var size = p.Size * Scale;
				byte directionMode = 0;


				v.TexCoord0 = new Vector4( size.x, size.y, p.SequenceTime, 0 );
				v.TexCoord1 = p.Color.WithAlphaMultiplied( p.Alpha );

				v.Position = p.Position;

				v.Normal.x = p.Angles.pitch;
				v.Normal.y = p.Angles.yaw;
				v.Normal.z = p.Angles.roll;

				v.Tangent.x = p.Velocity.x;
				v.Tangent.y = p.Velocity.y;
				v.Tangent.z = p.Velocity.z;


				v.Color.r = (byte)p.Sequence;
				v.Color.g = directionMode;

				_so.AddVertex( v );

				v.Position += p.Velocity * (1.0f / 500.0f) * size.x;
			}

			// expand bounds slightly, based on max particle size?
			bounds.Mins -= Vector3.One * 64;
			bounds.Maxs += Vector3.One * 64;

			_so.Bounds = bounds;
		}
	}
}
