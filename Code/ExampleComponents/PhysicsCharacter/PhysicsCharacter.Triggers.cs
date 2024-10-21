
public sealed partial class PhysicsCharacter
{
	void CategorizeTriggers()
	{
		if ( !BodyCollider.IsValid() ) return;

		var wt = WorldTransform;
		Vector3 head = wt.PointToWorld( new Vector3( 0, 0, BodyHeight ) );
		Vector3 foot = wt.Position;

		float waterLevel = 0;

		foreach ( var touch in Body.Touching )
		{
			if ( touch.Tags.Contains( "water" ) )
			{
				var waterSurface = touch.FindClosestPoint( head );
				var level = Vector3.InverseLerp( waterSurface, foot, head, true );
				level = (level * 100).CeilToInt() / 100.0f;

				if ( level > waterLevel )
					waterLevel = level;
			}
		}

		if ( WaterLevel != waterLevel )
		{
			WaterLevel = waterLevel;
		}
	}
}
