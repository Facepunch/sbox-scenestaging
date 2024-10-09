public sealed partial class PhysicalCharacterController : Component
{
	[Property, ToggleGroup( "StepUp" )] public bool StepUp { get; set; } = true;
	[Property, Group( "StepUp" )] public bool StepDebug { get; set; } = true;
	[Property, Group( "StepUp" )] public float StepHeight { get; set; } = 18.0f;

	void TryStep()
	{
		if ( !StepUp ) return;
		if ( !IsOnGround ) return;
		if ( TimeSinceUngrounded < 0.2f ) return;
		if ( WishVelocity.IsNearlyZero( 0.001f ) ) return;

		var vel = (Velocity).WithZ( 0 ) * Time.Delta;
		if ( vel.IsNearlyZero( 0.1f ) ) return;

		var skin = 0.001f;
		var footbox = BBox.FromPositionAndSize( new Vector3( 0, 0, BodyHeight * 0.5f ), new Vector3( BodyRadius, BodyRadius, BodyHeight ) );
		var from = WorldTransform.Position + Vector3.Up * skin;

		//
		// Keep moving
		//

		var tr = Scene.Trace.Box( footbox, from - vel.Normal, from + vel ).IgnoreGameObjectHierarchy( GameObject ).Run();
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
		var hitDistance = tr.Distance - 1;
		var moveDir = vel.Normal * (vel.Length - hitDistance);
		from = tr.EndPosition;

		// move up 
		tr = Scene.Trace.Box( footbox, from + Vector3.Up * 2, from + Vector3.Up * StepHeight ).IgnoreGameObjectHierarchy( GameObject ).Run();
		if ( tr.Hit && (tr.Distance < 0.1f || tr.StartedSolid) )
		{
			DebugDrawSystem.Current.AddBox( footbox, new Transform( from + Vector3.Up * 1 ) ).WithColor( Color.Red );
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

			Body.WorldPosition = tr.EndPosition - Vector3.Up * 0.1f;

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
}
