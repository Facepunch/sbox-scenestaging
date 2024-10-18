public sealed partial class PhysicalCharacterController : Component
{
	[Property, ToggleGroup( "StepUp" )] public bool StepUp { get; set; } = true;
	[Property, Group( "StepUp" )] public bool StepDebug { get; set; } = true;
	[Property, Group( "StepUp" )] public float StepHeight { get; set; } = 18.0f;

	bool _didstep;
	Vector3 _stepPosition;

	SceneTraceResult TraceBody( Vector3 from, Vector3 to )
	{
		var tx = WorldTransform;
		return Scene.Trace.Sweep( Body, tx.WithPosition( from ), tx.WithPosition( to ) ).IgnoreGameObjectHierarchy( GameObject ).Run();
	}


	void TryStep()
	{
		_didstep = false;

		if ( !StepUp ) return;
		if ( !IsOnGround ) return;
		if ( TimeSinceUngrounded < 0.2f ) return;
		if ( WishVelocity.IsNearlyZero( 0.001f ) ) return;

		var vel = (Velocity).WithZ( 0 ) * Time.Delta;
		if ( vel.IsNearlyZero( 0.001f ) ) return;

		Reground();

		var footbox = BBox.FromPositionAndSize( new Vector3( 0, 0, BodyHeight * 0.5f ), new Vector3( BodyRadius, BodyRadius, BodyHeight ) );
		var from = WorldTransform.Position + Vector3.Up * skin;

		//
		// Keep moving
		//

		var tr = Scene.Trace.Box( footbox, from - vel.Normal * skin, from + vel ).IgnoreGameObjectHierarchy( GameObject ).Run();
		if ( !tr.Hit )
		{
			var box = footbox.Translate( from );
			box = box.AddBBox( footbox.Translate( from + vel ) );

			if ( StepDebug )
				DebugDrawSystem.Current.AddBox( box ).WithColor( Color.Green );

			return;
		}

		//
		// We hit a step
		//
		var hitDistance = tr.Distance - skin * 2;
		var moveDir = vel.Normal * (vel.Length - hitDistance);
		from = from + vel.Normal * hitDistance;

		// move up 
		tr = Scene.Trace.Box( footbox, from, from + Vector3.Up * StepHeight ).IgnoreGameObjectHierarchy( GameObject ).Run();
		if ( tr.Hit && tr.StartedSolid )
		{
			DebugDrawSystem.Current.AddBox( footbox, new Transform( from + Vector3.Up * 1 ) ).WithColor( Color.Red ).WithTime( 30 );
			return;
		}

		// move across
		var fromupper = from += Vector3.Up * (tr.Distance + 1);
		tr = Scene.Trace.Box( footbox, fromupper, fromupper + moveDir ).IgnoreGameObjectHierarchy( GameObject ).Run();
		if ( tr.StartedSolid || tr.Distance < 0.1f )
			return;

		var dist = tr.Distance;

		//
		// We can go across
		// 

		tr = Scene.Trace.Box( footbox, tr.EndPosition, tr.EndPosition + Vector3.Down * StepHeight ).IgnoreGameObjectHierarchy( GameObject ).Run();
		if ( tr.Hit )
		{
			if ( !CanStandOnSurfaceNormal( tr.Normal ) )
				return;

			_didstep = true;
			_stepPosition = tr.EndPosition - Vector3.Up * skin;

			Body.WorldPosition = _stepPosition;


			if ( StepDebug )
			{
				var box = footbox.Translate( tr.EndPosition );
				DebugDrawSystem.Current.AddBox( box ).WithColor( Color.Cyan ).WithTime( 10.0f );
			}

			return;
		}

		if ( StepDebug )
		{
			DebugDrawSystem.Current.AddBox( footbox.Translate( tr.HitPosition ) ).WithColor( Color.Red ).WithTime( 10 );
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
