using Sandbox;
using System;

internal class FixedUpdate
{
	public float Frequency = 16;
	public float MaxSteps = 5;
	public float Delta => 1.0f / Frequency;

	float lastTime;

	int ticks = 0;

	internal void Run( Action fixedUpdate )
	{
		var delta = Delta;
		var time = Time.Now;
		lastTime = lastTime.Clamp( time - (MaxSteps * delta), time + delta );

		while ( lastTime < time )
		{
			using var timeScope = Time.Scope( lastTime, delta, ticks );

			Time.Now = lastTime;

			fixedUpdate();
			
			lastTime += delta;
			ticks++;
		}
	}
}
