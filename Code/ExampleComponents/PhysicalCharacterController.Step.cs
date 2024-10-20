public sealed partial class PhysicalCharacterController : Component
{
	[Property, ToggleGroup( "StepUp" )] public bool StepUp { get; set; } = true;
	[Property, Group( "StepUp" )] public bool StepDebug { get; set; } = true;
	[Property, Group( "StepUp" )] public float StepHeight { get; set; } = 18.0f;

	bool _didstep;
	Vector3 _stepPosition;

	BBox BodyBox( float scale = 1.0f, float heightScale = 1.0f ) => new BBox( new Vector3( -BodyRadius * 0.5f * scale, -BodyRadius * 0.5f * scale, 0 ), new Vector3( BodyRadius * 0.5f * scale, BodyRadius * 0.5f * scale, BodyHeight * heightScale ) );

	SceneTraceResult TraceBody( Vector3 from, Vector3 to, float scale = 1.0f, float heightScale = 1.0f )
	{
		var tx = WorldTransform;
		//return Scene.Trace.Sweep( Body, tx.WithPosition( from ), tx.WithPosition( to ) ).IgnoreGameObjectHierarchy( GameObject ).Run();

		//var bbox = new BBox( new Vector3( BodyRadius, BodyRadius, 0 ), new Vector3( BodyRadius, BodyRadius, BodyHeight ) );

		return Scene.Trace.Box( BodyBox( scale, heightScale ), from, to ).IgnoreGameObjectHierarchy( GameObject ).Run();
	}


	void TryStep()
	{
		_didstep = false;

		if ( !StepUp ) return;
		//if ( !IsOnGround ) return;
		//if ( TimeSinceUngrounded < 0.2f ) return;
		//if ( WishVelocity.IsNearlyZero( 0.001f ) ) return;
		if ( Velocity.WithZ( 0 ).IsNearlyZero( 0.0001f ) ) return;

		Reground();

		var from = WorldPosition;
		var vel = Velocity.WithZ( 0 ) * Time.Delta;
		float radiusScale = 1.0f;

		//
		// Keep moving
		//

		//var tr = Scene.Trace.Box( footbox, from - vel.Normal * skin, from + vel ).IgnoreGameObjectHierarchy( GameObject ).Run();
		var tr = TraceBody( from - vel.Normal * skin, from + vel, radiusScale );

		while ( tr.StartedSolid )
		{
			radiusScale = radiusScale - 0.1f;
			if ( radiusScale < 0.6f )
				return;

			tr = TraceBody( from - vel.Normal * skin, from + vel, radiusScale );
		}

		if ( tr.StartedSolid )
		{
			//	Log.Info( $"Started Solid: {from} to {vel}" );
			//	DebugDrawSystem.Current.AddBox( BBox.FromPositionAndSize( from, 3 ) ).WithColor( Color.Orange ).WithTime( 5 );
			return;
		}

		if ( !tr.Hit )
			return;

		//
		// We hit a step
		//
		var hitDistance = tr.Distance - skin * 2;
		var moveDir = vel.Normal * (vel.Length - hitDistance);
		from = from + vel.Normal * (tr.Distance - skin);

		// move up 
		//tr = Scene.Trace.Box( footbox, from, from + Vector3.Up * StepHeight ).IgnoreGameObjectHierarchy( GameObject ).Run();
		tr = TraceBody( from, from + Vector3.Up * StepHeight, radiusScale );
		if ( tr.Hit && !tr.StartedSolid )
		{
			//DebugDrawSystem.Current.AddBox( BBox.FromPositionAndSize( tr.HitPosition, 3 ) ).WithColor( Color.Red ).WithTime( 30 );
			return;
		}

		// move across
		var fromupper = from += Vector3.Up * (tr.Distance + 1);
		tr = TraceBody( fromupper, fromupper + moveDir, radiusScale );
		if ( tr.Hit )
			return;

		var dist = tr.Distance;

		//
		// Step Down
		// 
		{
			var top = tr.EndPosition;
			var bottom = tr.EndPosition + Vector3.Down * StepHeight;

			tr = TraceBody( tr.EndPosition, tr.EndPosition + Vector3.Down * StepHeight, radiusScale );
			if ( tr.Hit )
			{
				if ( !CanStandOnSurfaceNormal( tr.Normal ) )
					return;

				_didstep = true;
				_stepPosition = tr.EndPosition + Vector3.Up * skin;
				var oldPosition = Body.WorldPosition;

				Body.WorldPosition = _stepPosition;

				//if ( StepDebug )
				{
					DebugDrawSystem.Current.Line( oldPosition, _stepPosition, duration: 10 );
				}

				return;
			}
		}

		if ( StepDebug )
		{
			DebugDrawSystem.Current.Box( tr.HitPosition, 3, Color.Red, 10 );
		}

		//DebugDrawSystem.Current.AddLine( from, tr.EndPosition, 0.4f ).WithColor( Color.Red ).WithTime( 1.4f );
	}

	void RestoreStep()
	{
		if ( _didstep )
		{
			Body.WorldPosition = _stepPosition;
			_didstep = false;
		}
	}
}
