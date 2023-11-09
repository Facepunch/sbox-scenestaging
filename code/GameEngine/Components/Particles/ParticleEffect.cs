using Editor;
using Sandbox;
using Sandbox.Utility;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.Serialization;

namespace Sandbox;

[Title( "Particle Effect" )]
[Category( "Effects" )]
[Icon( "shower" )]
[EditorHandle( "materials/gizmo/particles.png" )]
public sealed class ParticleEffect : BaseComponent, BaseComponent.ExecuteInEditor
{
	[Property, Range( 0, 2 )] public float Speed { get; set; } = 1.0f;

	[Property] public Vector3 Force { get; set; }
	[Property] public bool Collision { get; set; }
	[Property, Range( 0, 1 )] public float Damping { get; set; }

	[Property] public int MaxParticles { get; set; } = 1000;
	[Property] public Color Tint { get; set; } = Color.White;

	public List<Particle> Particles { get; } = new List<Particle>();

	[Property] public Curve Lifetime { get; set; } = 1.0f;
	[Property] public Curve AlphaOverLifetime { get; set; } = 1.0f;

	[Property] public Curve StartRotation { get; set; } = 0.0f;
	[Property] public Curve StartVelocity { get; set; } = 1.0f;

	[Property, Range( 0, 1 )] public float SimulationSpace { get; set; } = 1.0f;
	[Property, Range( 0, 1 )] public float SequenceSpeed { get; set; } = 1.0f;


	public bool IsFull => Particles.Count >= MaxParticles;

	Transform lastTransform;

	ConcurrentQueue<Particle> deleteList = new ConcurrentQueue<Particle>();

	public override void Update()
	{
		using var ps = Superluminal.Scope( "Particle Effect", Color.Red, $"{GameObject.Name} - {Particles.Count} Particles" );


		float timeDelta = MathX.Clamp( Time.Delta, 0.0f, 1.0f / 30.0f ) * Speed;

		var tx = Transform.World;
		Vector3 lastPos = lastTransform.Position;
		Transform deltaTransform = tx.ToLocal( lastTransform );

		bool parentMoved = deltaTransform != global::Transform.Zero;

		Parallel.ForEach( Particles, p =>
		{
			float delta = MathX.Remap( Time.Now, p.BornTime, p.DeathTime );

			if ( parentMoved && p.Frame > 0 && SimulationSpace > 0.0f )
			{
				var localPos = lastTransform.PointToLocal( p.Position );
				var worldPos = tx.PointToWorld( localPos );

				p.Position = Vector3.Lerp( p.Position, worldPos, SimulationSpace );
			}

			p.Frame++;


			if ( !Force.IsNearlyZero() )
			{
				p.Velocity += Force * timeDelta;
			}

			if ( Damping != 0 )
			{
				p.Velocity -= p.Velocity * timeDelta * Damping;
			}

			var target = p.Position + (p.Velocity * timeDelta);

			if ( Collision )
			{
				var tr = global::Physics.Trace.Ray( p.Position, target ).Radius( p.Radius ).Run();

				if ( tr.Hit )
				{
					p.Velocity = Vector3.Reflect( p.Velocity, tr.Normal ) * Random.Shared.Float( 0.6f, 0.9f );
					target = tr.EndPosition;
				}
			}

			p.Position = target;
			p.Size = 1.0f;
			p.Alpha = AlphaOverLifetime.Evaluate( delta );
			p.SequenceTime += timeDelta * SequenceSpeed;

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

	static void TickParticle()
	{

	}

	public Particle Emit( Vector3 position )
	{
		var p = Particle.Create();

		p.Position = position;
		p.Radius = 1.0f;
		p.DeathTime = Time.Now + Lifetime.Evaluate( Random.Shared.Float( 0, 1 ) );
		p.Color = Tint;
		p.Angles.roll = StartRotation.Evaluate( Random.Shared.Float( 0, 1 ) );
		p.Velocity = Vector3.Random.Normal * StartVelocity.Evaluate( Random.Shared.Float( 0, 1 ) );

		Particles.Add( p );

		return p;		
	}

	public void Terminate( Particle p )
	{
		Particles.Remove( p );
		Particle.Pool.Enqueue( p );
	}
}
