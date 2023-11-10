using System;
using System.Collections.Generic;

namespace Sandbox;

public class Particle
{
	public Vector3 Position;
	public Vector3 Size;
	public Vector3 Velocity;
	public Color Color;
	public float Alpha;
	public float BornTime;
	public float Age;
	public float Radius;
	public Angles Angles;
	public int Sequence;
	public float SequenceTime;
	public int Frame;

	public float Random01;
	public float Random02;
	public float Random03;
	public float Random04;
	public float Random05;
	public float Random06;
	public float Random07;

	public static Queue<Particle> Pool = new ( 512 );

	public static Particle Create()
	{
		if ( !Pool.TryDequeue( out Particle p ) )
		{
			p = new Particle();
		}

		p.Random01 = Random.Shared.Float( 0, 1 );
		p.Random02 = Random.Shared.Float( 0, 1 );
		p.Random03 = Random.Shared.Float( 0, 1 );
		p.Random04 = Random.Shared.Float( 0, 1 );
		p.Random05 = Random.Shared.Float( 0, 1 );
		p.Random06 = Random.Shared.Float( 0, 1 );
		p.Random07 = Random.Shared.Float( 0, 1 );

		p.BornTime = Time.Now;
		p.Age = 0;
		p.Angles = Angles.Zero;
		p.Frame = 0;
		p.Velocity = 0;
		p.Color = Color.White;
		p.Alpha = 1;
		p.Sequence = 0;
		p.SequenceTime = Random.Shared.Float( 0, 100 );
		p.Size = 5;

		return p;
	}
}
