namespace Sandbox;

/// <summary>
/// Implements logic for the SoundScape system
/// </summary>
class SceneSoundscapeSystem : GameObjectSystem
{
	public SceneSoundscapeSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.UpdateBones, 0, Update, "TickSoundScapes" );
	}

	SoundscapeTrigger active;
	RealTimeSince timeSinceUpdate;

	void Update()
	{
		if ( Scene.IsEditor )
			return;

		if ( timeSinceUpdate < 0.2f )
			return;

		timeSinceUpdate = 0;

		var head = Sound.Listener ?? Transform.Zero;

		var ambience = Scene.GetAllComponents<SoundscapeTrigger>()
								.Where( x => x.TestListenerPosition( head.Position ) )
								.OrderBy( x => x.Transform.Position.DistanceSquared( head.Position ) );

		var best = ambience.FirstOrDefault();
		
		if ( best == active )
			return;

		if ( best == null && active.StayActiveOnExit )
			return;

		if ( active is not null ) 
			active.Playing = false;

		active = best;

		if ( active is not null ) 
			active.Playing = true;
		

	}
}
