namespace Sandbox;

/// <summary>
/// Emits particles in a line. The line can be flat or have a tube-like quality.
/// </summary>
[Title( "Line Emitter" )]
[Category( "Particles" )]
[Icon( "stacked_line_chart" )]
public sealed class ParticleLineEmitter : ParticleEmitter
{
	[Group( "Line" )]
	[Property] public Vector3 TargetPosition { get; set; } = new Vector3( 500, 0, 0 );

	[Group( "Line" )]
	[Property] public ParticleFloat Thickness { get; set; } = 0.0f;

	[Group( "Placement" )]
	[Property] public float MinDistance { get; set; } = 0.0f;

	[Group( "Placement" )]
	[Property]
	public bool PlaceRandomly { get; set; }

	[Group( "Placement" )]
	[Property]
	public bool PlaceAdvanced { get; set; }

	[ShowIf( "PlaceAdvanced", true )]
	[Group( "Placement" )]
	[Property]
	public ParticleFloat EmitLinePosition { get; set; } = new ParticleFloat { Type = ParticleFloat.ValueType.Range, Evaluation = ParticleFloat.EvaluationType.Life, ConstantA = 0, ConstantB = 1 };


	[Group( "Velocity" )][Property] public bool ApplyVelocity { get; set; }
	[ShowIf( "ApplyVelocity", true )][Group( "Velocity" )][Property] public bool ScaleByDistance { get; set; }
	[ShowIf( "ScaleByDistance", true )][Group( "Velocity" )][Property] public ParticleFloat FixedVelocity { get; set; } = 100.0f;
	[ShowIf( "ApplyVelocity", true )][Group( "Velocity" )][Property] public float VelocityRandom { get; set; }







	protected override void DrawGizmos()
	{
		base.DrawGizmos();

		if ( Gizmo.IsSelected )
		{
			Gizmo.Draw.Color = Color.White.WithAlpha( 0.05f );
			Gizmo.Draw.Line( 0, TargetPosition );

			var pos = WorldTransform.PointToWorld( TargetPosition );

			using ( Gizmo.Scope( "Tool", new Transform( TargetPosition ) ) )
			{
				if ( Gizmo.Control.Position( "TargetPosition", 0, out var newPos ) )
				{
					TargetPosition += newPos;
				}
			}
		}

	}

	protected override int GetBurstCount()
	{
		var count = base.GetBurstCount();
		if ( count <= 0 ) return 0;

		Vector3 from = WorldTransform.Position;
		Vector3 to = WorldTransform.PointToWorld( TargetPosition );
		var moveDelta = (to - from).Length;

		var minDistance = MinDistance;
		if ( minDistance > 0.5f )
		{
			int minCount = (int)(moveDelta / minDistance);
			if ( count < minCount ) count = minCount;
		}

		return count;
	}

	protected override int GetRateCount()
	{
		var count = base.GetRateCount();
		if ( count <= 0 ) return 0;

		Vector3 from = WorldPosition;
		Vector3 to = WorldTransform.PointToWorld( TargetPosition );
		var moveDelta = (to - from).Length;

		var minDistance = MinDistance;
		if ( minDistance > 0.5f )
		{
			int minCount = (int)(moveDelta / minDistance);
			if ( count < minCount ) count = minCount;
		}

		return count;
	}


	public override bool Emit( ParticleEffect target )
	{
		Vector3 from = WorldPosition;
		Vector3 to = WorldTransform.PointToWorld( TargetPosition );
		var moveDelta = to - from;
		var lineNormal = moveDelta.Normal;

		var delta = Delta;

		if ( PlaceRandomly )
		{
			delta = Random.Shared.Float( 0, 1 );
		}
		else if ( PlaceAdvanced )
		{
			delta = EmitLinePosition.Evaluate( delta, Random.Shared.Float( 0, 1 ) );
		}

		var pos = from.LerpTo( to, delta );

		float thickness = Thickness.Evaluate( Delta, 63456 );
		if ( thickness != 0.0f )
		{
			pos += Vector3.Random.SubtractDirection( lineNormal ) * thickness;
		}

		var p = target.Emit( pos, Random.Shared.Float( 0, 1 ) );

		if ( p is not null )
		{
			if ( ApplyVelocity )
			{
				p.Position = from;

				var velocity = (to - from) / p.LifeTimeRemaining;
				p.Velocity = velocity;

				p.Velocity += VelocityRandom * Vector3.Random;

				if ( ScaleByDistance )
				{
					p.TimeScale = FixedVelocity.Evaluate( Delta, 1336 ) / moveDelta.Length;
				}
			}
		}

		return true;
	}
}
