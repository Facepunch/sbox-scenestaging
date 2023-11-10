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

	public int Seed;

	public static Queue<Particle> Pool = new ( 512 );

	public static Particle Create()
	{
		if ( !Pool.TryDequeue( out Particle p ) )
		{
			p = new Particle();
		}

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
		p.Seed = Random.Shared.Int( 0, 1000 );

		return p;
	}
}
