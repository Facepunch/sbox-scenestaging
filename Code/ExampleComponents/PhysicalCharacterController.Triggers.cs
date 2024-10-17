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

			ladder = ladder || touch.Tags.Contains( "ladder" );
		}

		if ( OnLadder != ladder )
		{
			OnLadder = ladder;
			Log.Info( $"Ladder: {OnLadder}" );
		}

		if ( WaterLevel != waterLevel )
		{
			WaterLevel = waterLevel;
			//Log.Info( $"WaterLevel: {WaterLevel}" );
		}

		Body.Gravity = !IsSwimming && !OnLadder;
		Body.LinearDamping = Body.Gravity ? 0 : 2;
	}
}
