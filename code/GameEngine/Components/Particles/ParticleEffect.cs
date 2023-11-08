using Editor;
using Sandbox;
using Sandbox.Utility;
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


	public bool IsFull => Particles.Count >= MaxParticles;

	public override void Update()
	{
		Action deferredAction = default;

		float timeDelta = MathX.Clamp( Time.Delta, 0.0f, 1.0f / 30.0f ) * Speed;

		Parallel.ForEach( Particles, p =>
		{
			float delta = MathX.Remap( Time.Now, p.BornTime, p.DeathTime );

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
			p.Color = p.Color.WithAlpha( AlphaOverLifetime.Evaluate( delta ) );
			

			if ( delta >= 1.0f )
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

	public Particle Emit( Vector3 position )
	{
		var p = new Particle();

		p.Position = position;
		p.Radius = 4.0f;
		p.BornTime = Time.Now;
		p.DeathTime = Time.Now + Lifetime.Evaluate( Random.Shared.Float( 0, 1 ) );
		p.Color = Tint;
		p.Angles.roll = StartRotation.Evaluate( Random.Shared.Float( 0, 1 ) );

		Particles.Add( p );

		return p;		
	}

	public void Terminate( Particle p )
	{
		Particles.Remove( p );
	}
}
