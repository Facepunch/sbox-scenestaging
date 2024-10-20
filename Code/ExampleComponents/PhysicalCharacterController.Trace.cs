
public sealed partial class PhysicalCharacterController : Component
{

	void CategorizeTriggers()
	{
		var bodyCollider = Body.GetComponent<CapsuleCollider>();
		if ( bodyCollider is null ) return;

		var wt = WorldTransform;
		Vector3 head = wt.PointToWorld( new Vector3( 0, 0, BodyHeight ) );
		Vector3 foot = wt.Position;

		float waterLevel = 0;
		bool ladder = false;
		GameObject ladderObject = default;

		foreach ( var touch in Body.Touching )
		{
			if ( touch.Tags.Contains( "water" ) )
			{
				var waterSurface = touch.FindClosestPoint( head );
				var level = Vector3.InverseLerp( waterSurface, foot, head, true );
				level = (level * 100).CeilToInt() / 100.0f;

				//DebugDrawSystem.Current.AddLine( waterSurface, head ).WithTime( 0.5f ).WithColor( Color.Green );
				//DebugDrawSystem.Current.AddLine( foot, waterSurface ).WithTime( 0.5f ).WithColor( Color.Blue );

				if ( level > waterLevel )
					waterLevel = level;
			}

			if ( ladder == false && touch.Tags.Contains( "ladder" ) )
			{
				var ladderSurface = touch.FindClosestPoint( head );
				var level = Vector3.InverseLerp( ladderSurface, foot, head, true );

				// Don't start climbing this ladder if it's below us, and we're not already climbing it
				if ( ClimbingObject != touch.GameObject && level < 0.5f )
					continue;

				ladderObject = touch.GameObject;
			}
		}

		TryStartClimbing( ladderObject );

		if ( WaterLevel != waterLevel )
		{
			WaterLevel = waterLevel;
		}

		if ( IsSwimming )
		{
			Body.Gravity = false;
			Body.LinearDamping = 5;
		}
		else if ( IsClimbing )
		{
			Body.Gravity = false;
			Body.LinearDamping = 10;
		}
		else
		{
			Body.Gravity = true;
			Body.LinearDamping = 0.1f;
		}
	}
}
