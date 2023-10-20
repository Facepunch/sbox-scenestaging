using Sandbox;
using System;

internal class FixedUpdate
{
	public float Frequency = 16;
	public float MaxSteps = 5;
	public float Delta => 1.0f / Frequency;

	float lastTime;

	internal void Run( Action fixedUpdate )
	{
		var saveNow = Time.Now;
		var saveDelta = Time.Delta;

		var delta = Delta;
		var time = saveNow;
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
