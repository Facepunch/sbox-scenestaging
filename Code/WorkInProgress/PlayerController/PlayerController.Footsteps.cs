namespace Sandbox;

public sealed partial class PlayerController : Component
{
	/// <summary>
	/// Draw debug overlay on footsteps
	/// </summary>
	public bool DebugFootsteps;

	TimeSince _timeSinceStep;

	private void OnFootstepEvent( SceneModel.FootstepEvent e )
	{
		if ( !IsOnGround ) return;
		if ( _timeSinceStep < 0.2f ) return;

		_timeSinceStep = 0;

		PlayFootstepSound( e.Transform.Position, e.Volume, e.FootId );
	}

	public void PlayFootstepSound( Vector3 worldPosition, float volume, int foot )
	{
		var tr = Scene.Trace
			.Ray( worldPosition + Vector3.Up * 10, worldPosition + Vector3.Down * 20 )
			.IgnoreGameObjectHierarchy( GameObject )
			.Run();



		if ( !tr.Hit || tr.Surface is null )
		{
			if ( DebugFootsteps )
			{
				DebugOverlay.Sphere( new Sphere( worldPosition, volume ), duration: 10, color: Color.Red, overlay: true );
			}

			return;
		}

		var sound = foot == 0 ? tr.Surface.Sounds.FootLeft : tr.Surface.Sounds.FootRight;
		var soundEvent = ResourceLibrary.Get<SoundEvent>( sound );
		if ( soundEvent is null )
		{
			if ( DebugFootsteps )
			{
				DebugOverlay.Sphere( new Sphere( worldPosition, volume ), duration: 10, color: Color.Orange, overlay: true );
			}

			return;
		}

		var handle = GameObject.PlaySound( soundEvent, 0 );
		handle.TargetMixer = FootstepMixer.GetOrDefault();
		handle.Volume *= volume * FootstepVolume;

		if ( DebugFootsteps )
		{
			DebugOverlay.Sphere( new Sphere( worldPosition, volume ), duration: 10, overlay: true );
			DebugOverlay.Text( worldPosition, $"{soundEvent.ResourceName}", size: 14, flags: TextFlag.LeftTop, duration: 10, overlay: true );
		}
	}
}
