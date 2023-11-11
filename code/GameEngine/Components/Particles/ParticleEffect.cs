using Editor;
using Sandbox;
using Sandbox.Utility;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;

namespace Sandbox;

[Title( "Particle Effect" )]
[Category( "Effects" )]
[Icon( "shower" )]
[EditorHandle( "materials/gizmo/particles.png" )]
public sealed class ParticleEffect : BaseComponent, BaseComponent.ExecuteInEditor
{
	[Property, Group( "Limits" )]
	public int MaxParticles { get; set; } = 1000;

	[Property, Group( "Limits" )]
	public ParticleFloat Lifetime { get; set; } = 1.0f;

	[Property, Group( "Time" ), Range( 0, 1 )]
	public float TimeScale { get; set; } = 1.0f;

	[Property, Group( "Time" )]
	public ParticleFloat PerParticleTimeScale { get; set; } = 1.0f;


	[Property, Group( "Movement" )]
	public ParticleFloat StartVelocity { get; set; } = 1.0f;
	[Property, Group( "Movement" )]
	public ParticleFloat Damping { get; set; } = 0.0f;
	[Property, Group( "Movement" )]
	public SimulationSpace Space { get; set; }

	[Property, ToggleGroup( "ApplyRotation", Label = "Rotation" )]
	public bool ApplyRotation { get; set; } = false;

	[Property, Group( "ApplyRotation" )]
	public ParticleFloat Roll { get; set; } = 0.0f;



	[Property, ToggleGroup( "ApplyShape", Label = "Shape" )]
	public bool ApplyShape { get; set; } = false;

	[Property, Group( "ApplyShape" )]
	public ParticleFloat Scale { get; set; } = 1.0f;

	[Property, Group( "ApplyShape" )]
	public ParticleFloat Stretch { get; set; } = 0.0f;


	[Property, ToggleGroup( "ApplyColor", Label = "Color" )]
	public bool ApplyColor { get; set; } = false;

	[Property, Group( "ApplyColor" )]
	public Color Tint { get; set; } = Color.White;

	[Property, Group( "ApplyColor" )]
	public Gradient Gradient { get; set; } = Color.White;

	[Property, Group( "ApplyColor" )]
	public ParticleFloat Brightness { get; set; } = 1.0f;	
	
	[Property, Group( "ApplyColor" )]
	public ParticleFloat Alpha { get; set; } = 1.0f;


	[Property, ToggleGroup( "Force" )]
	public bool Force { get; set; }

	[Property, Group( "Force" )]
	public Vector3 ForceDirection { get; set; }

	[Property, Group( "Force" )]
	public ParticleFloat ForceScale { get; set; } = 1.0f;


	[Property, Range( 0, 1 )] public float SequenceSpeed { get; set; } = 1.0f;

	[Property, ToggleGroup( "Collision" )] 
	public bool Collision { get; set; }

	[Property, ToggleGroup( "Collision" )]
	public ParticleFloat DieOnCollisionChance { get; set; } = 0.0f;

	[Property, ToggleGroup( "Collision" )]
	public float CollisionRadius { get; set; }

	[Property, ToggleGroup( "Collision" )]
	public TagSet CollisionIgnore { get; set; }

	[Property, ToggleGroup( "Collision" )]
	public ParticleFloat Bounce { get; set; } = 1.0f;

	[Property, ToggleGroup( "Collision" )]
	public ParticleFloat Friction { get; set; } = 1.0f;

	[Property, ToggleGroup( "Collision" )]
	public ParticleFloat Bumpiness { get; set; } = 0.0f;

	[Property, ToggleGroup( "Collision" )]
	public ParticleFloat PushStrength { get; set; } = 0.0f;


	public List<Particle> Particles { get; } = new List<Particle>();

	public bool IsFull => Particles.Count >= MaxParticles;

	Transform lastTransform;

	ConcurrentQueue<Particle> deleteList = new ConcurrentQueue<Particle>();

	public enum SimulationSpace
	{
		World,
		Local
	}

	public override void Update()
	{
		using var ps = Superluminal.Scope( "Particle Effect", Color.Red, $"{GameObject.Name} - {Particles.Count} Particles" );


		float timeDelta = MathX.Clamp( Time.Delta, 0.0f, 1.0f / 30.0f ) * TimeScale;

		var tx = Transform.World;
		Vector3 lastPos = lastTransform.Position;
		Transform deltaTransform = tx.ToLocal( lastTransform );

		bool parentMoved = deltaTransform != global::Transform.Zero;
		bool isEditor = Scene.IsEditor;

		Parallel.ForEach( Particles, p =>
		{
			var deathTime = p.BornTime + Lifetime.Evaluate( p.Random01, p.Random02 );

			float delta = MathX.Remap( p.BornTime + p.Age, p.BornTime, deathTime );


			var damping = Damping.Evaluate( delta, p.Random01 );
			var forceScale = ForceScale.Evaluate( delta, p.Random02 );
			var timeScale = PerParticleTimeScale.Evaluate( delta, p.Random03 ) * timeDelta;

			if ( parentMoved && p.Frame > 0 && Space == SimulationSpace.Local )
			{
				var localPos = lastTransform.PointToLocal( p.Position );
				var worldPos = tx.PointToWorld( localPos );

				p.Position = worldPos;
			}

			p.Age += timeScale;
			p.Frame++;


			p.ApplyDamping( damping * timeScale );

			if ( Force && forceScale != 0.0f && !ForceDirection.IsNearlyZero() )
			{
				p.Velocity += forceScale * ForceDirection * timeScale;
			}


			if ( Collision )
			{
				var bounce = Bounce.Evaluate( delta, p.Random07 );
				var friction = Friction.Evaluate( delta, p.Random06 );
				var bumpiness = Bumpiness.Evaluate( delta, p.Random05 );
				var push = PushStrength.Evaluate( delta, p.Random04 );
				var die = DieOnCollisionChance.Evaluate( delta, p.Random03 ) > 0.5f;

				if ( isEditor ) push = 0;

				p.MoveWithCollision( bounce, friction, bumpiness, push, die, timeScale );
			}
			else
			{
				p.Position += p.Velocity * timeScale;
			}

			if ( ApplyColor )
			{
				var brightness = Brightness.Evaluate( delta, p.Random01 );

				p.Alpha = Alpha.Evaluate( delta, p.Random02 );

				p.Color = Tint * Gradient.Evaluate( p.Random03 ); // TODO, gradient, between two gradients etc
				p.Color *= new Color( brightness, 1.0f );
			}

			if ( ApplyShape )
			{
				p.Size = Scale.Evaluate( delta, p.Random04 );

				var aspect = Stretch.Evaluate( delta, p.Random05 );
				if ( aspect < 0 )
				{
					p.Size.x *= aspect.Remap( 0, -1, 1, 2, false );
				}
				else if ( aspect > 0 )
				{
					p.Size.y *= aspect.Remap( 0, 1, 1, 2, false );
				}
			}

			if ( ApplyRotation )
			{
				p.Angles.roll = Roll.Evaluate( delta, p.Random06 );
			}


			p.SequenceTime += timeScale * SequenceSpeed;

			if ( delta >= 1.0f )
			{
				deleteList.Enqueue( p );
			}

		} );

		while ( deleteList.TryDequeue( out var delete ))
		{
			Terminate( delete );
		}

		lastTransform = tx;
	}

	public Particle Emit( Vector3 position )
	{
		var p = Particle.Create();

		p.Position = position;
		p.Radius = 1.0f;
		p.Velocity = Vector3.Random.Normal * StartVelocity.Evaluate( Random.Shared.Float( 0, 1 ), Random.Shared.Float( 0, 1 ) );


		Particles.Add( p );

		return p;		
	}

	public void Terminate( Particle p )
	{
		Particles.Remove( p );
		Particle.Pool.Enqueue( p );
	}
}

