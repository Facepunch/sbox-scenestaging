using Editor;
using Sandbox;
using Sandbox.Utility;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.Serialization;

public class Particle
{
	public Vector3 Position;
	public Vector3 Size;
	public Vector3 Velocity;
	public Color Color;
	public float BornTime;
	public float DeathTime;
	public float Radius;
}

public sealed class SpriteRenderer : BaseComponent, BaseComponent.ExecuteInEditor
{
	SpriteSceneObject _so;
	[Property] public Texture Texture { get; set; } = Texture.White;
	[Property] public Vector2 Size { get; set; } = 10.0f;
	[Property, Range( 0, 2 )] public float Scale { get; set; } = 1.0f;
	[Property, Range( 0, 2 )] public float Speed { get; set; } = 1.0f;
	[Property] public bool Additive { get; set; }

	[Property] public int MaxParticles { get; set; } = 1000;


	public List<Particle> Particles { get; } = new List<Particle>();

	public override void Update()
	{
		if ( Particles.Count < MaxParticles)
		{
			Emit( 1000 );
		}

		Action deferredAction = default;

		Vector3 gravityForce = Vector3.Down * 900.0f;

		float timeDelta = MathX.Clamp( Time.Delta, 0.0f, 1.0f / 30.0f ) * Speed;

		Parallel.ForEach( Particles, p =>
		{
			float delta = MathX.Remap( Time.Now, p.BornTime, p.DeathTime );

			p.Velocity += gravityForce * timeDelta;

			var target = p.Position + (p.Velocity * timeDelta);

			var tr = Physics.Trace.Ray( p.Position, target ).Radius( p.Radius ).Run();

			if ( tr.Hit )
			{
				p.Velocity = Vector3.Reflect( p.Velocity, tr.Normal ) * Random.Shared.Float( 0.6f, 0.9f );
				target = tr.EndPosition;
			}

			p.Position = target;
			p.Size = 1.0f * (1.0f - delta);

			if ( p.DeathTime <= Time.Now )
			{
				lock ( this )
				{
					deferredAction += () =>
					{
						Terminate( p );
					};
				}
			}

		} );

		deferredAction?.Invoke();
	}

	public void Emit( int count )
	{
		for( int i=0; i<count; i++ )
		{
			if ( Particles.Count >= MaxParticles )
				return;

			var p = new Particle();

			var pos = Vector3.Random;

			p.Position = Transform.World.Position + pos * 2.0f;
			p.Velocity = pos * 120.0f * Random.Shared.Float( 0.5f, 2.5f );
			p.Radius = 4.0f;
			p.BornTime = Time.Now;
			p.DeathTime = Time.Now + Random.Shared.Float( 1.6f, 5.9f );
			p.Color = Color.Random.Desaturate( 0.2f ).Darken( 0.8f );

			Particles.Add( p );
		}
	}

	public void Terminate( Particle p )
	{
		Particles.Remove( p );
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

	TimeSince timeSinceUpdate;

	protected override void OnPreRender()
	{
		if ( _so is null ) return;

		_so.Transform = Transform.World.WithRotation( Rotation.Identity );
		_so.Material = Material.FromShader( "sprite.vfx" );
		_so.Flags.CastShadows = true;
		_so.Texture = Texture;

		var left = Camera.Main.Rotation.Left;
		var up = Camera.Main.Rotation.Up;

		if ( Additive ) _so.Attributes.SetCombo( "D_BLEND", 1 );
		else _so.Attributes.SetCombo( "D_BLEND", 0 );

		timeSinceUpdate = 0;

		_so.Clear();

		foreach( var p in Particles )
		{
			var pos = _so.Transform.PointToLocal( p.Position );

			_so.DrawSprite( left, up, pos, p.Size * Scale, p.Color );
		}
	}
}

class SpriteSceneObject : SceneCustomObject
{
	public Material Material { get; set; }
	public Texture Texture { get; set; }
	public Vector2 Size { get; set; } = 10.0f;
	public float Scale { get; set; } = 1.0f;

	List<Vertex> Vertices = new List<Vertex>( 16 );

	public SpriteSceneObject( SceneWorld world ) : base( world )
	{
		RenderLayer = SceneRenderLayer.Default;
	}

	public override void RenderSceneObject()
	{
		if ( Material is null )
			return;

		if ( Texture is null )
			return;

		Attributes.Set( "BaseTexture", Texture );
		Attributes.Set( "BaseTextureSheet", Texture.SequenceData );

		Graphics.SetupLighting( this, Graphics.Attributes );
		Graphics.Draw( Vertices, Vertices.Count, Material, Attributes, Graphics.PrimitiveType.Triangles );
	}

	public void Clear()
	{
		Vertices.Clear();
	}

	public void DrawSprite( in Vector3 left, in Vector3 up, in Vector3 center, in Vector2 size, in Color color )
	{
		var l = left * size.x;
		var u = up * size.y;

		var a = center - l + u;
		var b = center + l + u;
		var c = center + l - u;
		var d = center - l - u;

		var va = new Vertex( a, new Vector4( 0, 0, 0, 0 ), color );
		var vb = new Vertex( b, new Vector4( 1, 0, 0, 0 ), color );
		var vc = new Vertex( c, new Vector4( 1, 1, 0, 0 ), color );
		var vd = new Vertex( d, new Vector4( 0, 1, 0, 0 ), color );

		lock ( this )
		{
			Vertices.AddRange( new Vertex[] { va, vb, vd, vb, vc, vd } );
		}
	}
}
