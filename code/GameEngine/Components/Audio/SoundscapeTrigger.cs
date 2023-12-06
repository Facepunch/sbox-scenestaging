using Sandbox;

[Title( "Soundscape Trigger" )]
[Category( "Rendering" )]
[Icon( "surround_sound" )]
[EditorHandle( "materials/gizmo/soundscape.png" )]
public class SoundscapeTrigger : Component
{
	public enum TriggerType
	{
		Point,
		Sphere,
		Box
	}

	[Property] public TriggerType Type { get; set; }

	[Property] public Soundscape Soundscape { get; set; }

	/// <summary>
	/// When true the soundscape will keep playinng after exiting the area, and will
	/// only stop playing once another soundscape takes over.
	/// </summary>
	[Property] public bool StayActiveOnExit { get; set; } = true;

	[Property]
	[ShowIf( nameof(Type), TriggerType.Sphere )]
	public float Radius { get; set; } = 500.0f;

	Vector3 _scale = 50;
	[Property]
	[ShowIf( nameof( Type ), TriggerType.Box )]
	public Vector3 BoxSize
	{
		get => _scale;
		set
		{
			if ( _scale == value ) return;

			_scale = value;
		}
	}

	protected override void DrawGizmos()
	{
		if ( Type == TriggerType.Point )
		{
			// nothing
		}
		else if( Type == TriggerType.Sphere )
		{
			if ( Gizmo.IsSelected )
			{
				Gizmo.Draw.Color = Playing ? Gizmo.Colors.Active : Gizmo.Colors.Blue;
				Gizmo.Draw.LineSphere( 0, Radius );
			}
		}
		else if(Type == TriggerType.Box)
		{
			if ( Gizmo.IsSelected )
			{
				Gizmo.Draw.Color = Playing ? Gizmo.Colors.Active : Gizmo.Colors.Blue;
				Gizmo.Draw.LineBBox( new BBox( -BoxSize, BoxSize ) );
			}
		}
		
	}


	public bool Playing { get; internal set; }

	bool wasPlaying;
	List<PlayingSound> activeEntries = new();
	List<PlayingSound> removalList = new();

	protected override void OnUpdate()
	{
		if ( Playing && !wasPlaying && Soundscape is not null )
		{
			StartSoundscape( Soundscape );
		}

		wasPlaying = Playing;

		if ( activeEntries.Count == 0 && removalList.Count == 0 )
			return;

		UpdateEntries( Sound.Listener );
	}

	protected override void OnDestroy()
	{
		foreach( var entry in activeEntries )
		{
			entry.Dispose();
		}
	}

	void UpdateEntries( Transform head )
	{
		foreach ( var e in activeEntries )
		{
			e.Frame( head );

			if ( !Playing )
				e.Finished = true;

			if ( e.IsDead )
				removalList.Add( e );			
		}

		foreach ( var e in removalList )
		{
			e.Dispose();
			activeEntries.Remove( e );
		}
	}

	/// <summary>
	/// Return true if they should hear this soundscape when in this position
	/// </summary>
	public bool TestListenerPosition( Vector3 position )
	{
		if ( Type == TriggerType.Sphere )
		{
			return (position - Transform.Position).LengthSquared < (Radius * Radius);
		}
		else if( Type == TriggerType.Box)
		{
			return new BBox( -BoxSize, BoxSize ).Contains( position - Transform.Position );
		}

		return true;
	}

	/// <summary>
	/// Load and start this soundscape..
	/// </summary>
	void StartSoundscape( Soundscape scape )
	{
		foreach ( var e in activeEntries )
		{
			e.Finished = true;
		}

		foreach ( var loop in scape.LoopedSounds )
			StartLoopedSound( loop, scape.MasterVolume.GetValue() );

		foreach ( var loop in scape.StingSounds )
			StartStingSound( loop, scape.MasterVolume.GetValue() );

	}

	void StartLoopedSound( Soundscape.LoopedSound sound, float masterVolume )
	{
		if ( sound?.SoundFile == null )
			return;

		foreach ( var entry in activeEntries.OfType<LoopedSoundEntry>() )
		{
			if ( entry.TryUpdateFrom( sound, masterVolume ) )
				return;
		}

		var e = new LoopedSoundEntry( sound, masterVolume );
		activeEntries.Add( e );
	}

	void StartStingSound( Soundscape.StingSound sound, float masterVolume )
	{
		if ( sound.SoundFile == null )
			return;

		for ( int i = 0; i < sound.InstanceCount; i++ )
		{
			var e = new StingSoundEntry( sound, masterVolume );
			activeEntries.Add( e );
		}
	}

	class PlayingSound : System.IDisposable
	{
		protected SoundHandle handle;
		protected float volumeScale = 1.0f;

		/// <summary>
		/// True if this sound has finished, can be removed
		/// </summary>
		internal virtual bool IsDead => !handle.IsPlaying && Finished;

		/// <summary>
		/// Gets set when it's time to fade this out
		/// </summary>
		public bool Finished { get; set; }

		public virtual void Frame( in Transform head ) { }

		public virtual void Dispose()
		{
			handle.Stop( true );
			handle = default;
		}
	}

	sealed class LoopedSoundEntry : PlayingSound
	{
		/// <summary>
		/// We store the current volume so we can seamlessly fade in and out
		/// </summary>
		public float currentVolume = 0.0f;

		/// <summary>
		/// Consider us dead if the soundscape system thinks we're finished and our volume is low
		/// </summary>
		internal override bool IsDead => currentVolume <= 0.001f && Finished;

		Soundscape.LoopedSound source;
		float sourceVolume;
		float soundVelocity = 0.0f;

		public LoopedSoundEntry( Soundscape.LoopedSound sound, float masterVolume )
		{
			currentVolume = 0.0f;
			volumeScale = masterVolume;

			handle = Sound.Play( "core.ambient" );

			handle.SetSoundFile( sound.SoundFile );

			UpdateFrom( sound );
		}

		public override void Frame( in Transform head )
		{
			var targetVolume = sourceVolume * volumeScale;
			if ( Finished ) targetVolume = 0.0f;

			currentVolume = MathX.SmoothDamp( currentVolume, targetVolume, ref soundVelocity, 5.0f, Time.Delta );
			handle.Volume = currentVolume;
		}

		public override string ToString() => $"Looped - Finished:{Finished} volume:{currentVolume:n0.00} - {source}";

		/// <summary>
		/// If we're using the same sound file as this incoming sound, and we're on our way out.. then
		/// let it replace us instead. This is much nicer.
		/// </summary>
		public bool TryUpdateFrom( Soundscape.LoopedSound sound, float masterVolume )
		{
			if ( !Finished ) return false;
			if ( sound.SoundFile != source.SoundFile ) return false;

			volumeScale = masterVolume;
			UpdateFrom( sound );
			return true;
		}

		void UpdateFrom( Soundscape.LoopedSound sound )
		{
			source = sound;
			sourceVolume = sound.Volume.GetValue();
			Finished = false;
		}
	}

	sealed class StingSoundEntry : PlayingSound
	{
		Soundscape.StingSound source;
		TimeUntil timeUntilNextShot;

		public StingSoundEntry( Soundscape.StingSound sound, float masterVolume )
		{
			source = sound;
			timeUntilNextShot = sound.RepeatTime.GetValue();
			volumeScale = masterVolume;
		}

		public override void Frame( in Transform head )
		{
			if ( Finished )
				return;

			if ( timeUntilNextShot > 0 )
				return;

			timeUntilNextShot = source.RepeatTime.GetValue();

			handle.Stop( false );
			handle = Sound.Play( source.SoundFile.ResourcePath );

			// we'll make this shape more configurable, but right now bias x/y rather than up and down
			var randomOffset = new Vector3( Game.Random.Float( -10, 10 ), Game.Random.Float( -10, 10 ), Game.Random.Float( -1, 1 ) );
			randomOffset = randomOffset.Normal * source.Distance.GetValue();

			handle.Position = head.Position + randomOffset;

			handle.Volume = volumeScale;
			
		}

	}
}

