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

	[Property] public int Count { get; set; } = 1;

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

		var trailMul = 1.0f / (float)Count;

		BBox bounds = BBox.FromPositionAndSize( _so.Transform.Position, 10 );

		using ( _so.Write( Graphics.PrimitiveType.Points, effect.Particles.Count * Count, 0 ) )
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
				var size = p.Size * Scale * p.Scale;

				v.Color.r = (byte) p.Sequence;

				v.TexCoord0 = new Vector4( size.x, size.y, p.SequenceTime, 0 );
				v.TexCoord1 = p.Color.WithAlphaMultiplied( p.Alpha );

				v.Position = p.Position;

				v.Normal.x = p.Angles.pitch;
				v.Normal.y = p.Angles.yaw;
				v.Normal.z = p.Angles.roll;


				for ( int i = 0; i < Count; i++ )
				{
					_so.AddVertex( v );

					v.Position += p.Velocity * (1.0f / 500.0f) * size.x;
					v.TexCoord1.w += trailMul;
				}
			}

			// expand bounds slightly, based on max particle size?
			bounds.Mins -= Vector3.One * 64;
			bounds.Maxs += Vector3.One * 64;

			_so.Bounds = bounds;
		}
	}
}
