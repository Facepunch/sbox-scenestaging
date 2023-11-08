using System.Collections.Generic;

namespace Sandbox;

public class Particle
{
	public Vector3 Position;
	public Vector3 Size;
	public Vector3 Velocity;
	public Color Color;
	public float BornTime;
	public float DeathTime;
	public float Radius;
	public Angles Angles;
	public int Frame;

	public static Queue<Particle> Pool = new ( 512 );

	public static Particle Create()
	{
		if ( !Pool.TryDequeue( out Particle p ) )
		{
			p = new Particle();
		}

		p.BornTime = Time.Now;
		p.Frame = 0;
		p.Velocity = 0;

		return p;
	}
}
