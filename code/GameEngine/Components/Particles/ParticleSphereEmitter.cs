using Editor;
using Sandbox;
using Sandbox.Utility;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.Serialization;

namespace Sandbox;

public sealed class ParticleSphereEmitter : BaseComponent, BaseComponent.ExecuteInEditor
{
	[Property, Range( 0, 1000 )] public float Initial { get; set; } = 10.0f;
	[Property, Range( 0, 1000 )] public float Rate { get; set; } = 1.0f;
	[Property, Range( 0, 100 )] public float Radius { get; set; } = 20.0f;
	[Property, Range( -1000, 1000 )] public float Velocity { get; set; } = 100.0f;


	public float time;

	public override void DrawGizmos()
	{
		Gizmo.Draw.Color = Color.White.WithAlpha( 0.1f );
		Gizmo.Draw.LineSphere( 0, Radius );
	}

	public override void OnEnabled()
	{
		time = Initial;
	}


	public override void Update()
	{
		if ( !TryGetComponent( out ParticleEffect effect ) ) return;

		time += Time.Delta * Rate;

		var center = Transform.Position;

		while ( !effect.IsFull && time >= 1.0f )
		{
			var offset = Vector3.Random;
			var pos = center + offset * Radius * Transform.Scale;


			var p = effect.Emit( pos );
			p.Velocity = offset * Velocity;

			time -= 1.0f;
		}
	}
}
