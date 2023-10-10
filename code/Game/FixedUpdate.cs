using Sandbox;
using System;

public class FixedUpdate
{
	public float Frequency = 32;
	public float MaxSteps = 5;
	public float Delta => 1.0f / Frequency;

	float lastTime;

	internal void Run( Action fixedUpdate )
	{
		var saveNow = Time.Now;
		var saveDelta = Time.Delta;

		var delta = Delta;
		var time = RealTime.Now;
		lastTime = lastTime.Clamp( time - (MaxSteps * delta), time + delta );

		

		Time.Delta = delta;

		while ( lastTime < time )
		{
			Time.Now = lastTime;

			fixedUpdate();
			
			lastTime += delta;
		}

		Time.Now = saveNow;
		Time.Delta = saveDelta;
	}
}
