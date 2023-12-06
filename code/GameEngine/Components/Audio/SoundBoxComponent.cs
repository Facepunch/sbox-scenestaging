using Sandbox;

[Category( "Audio" )]
[Title( "Sound Box" )]
[Icon( "surround_sound", "red", "white" )]
[EditorHandle( "materials/gizmo/sound.png" )]
public sealed class SoundBoxComponent : BaseSoundComponent
{
	[Property, Title( "Box Size" ), Group( "Box" )]
	public Vector3 Scale
	{
		get => _scale;
		set
		{
			if ( _scale == value ) return;

			_scale = value;
		}
	}
	Vector3 _scale = 50;

	public BBox Inner { get; private set; }

	public Vector3 SndPos { get; private set; }

	protected override void DrawGizmos()
	{
		if ( !Gizmo.IsSelected && !Gizmo.IsHovered )
			return;

		Gizmo.Draw.LineThickness = 1;
		Gizmo.Draw.Color = Gizmo.Colors.Green.WithAlpha( Gizmo.IsSelected ? 1.0f : 0.2f );
		Gizmo.Draw.LineBBox( new BBox( Scale * -0.5f, Scale * 0.5f ) );

		Gizmo.Draw.Color = Gizmo.Colors.Red.WithAlpha( 0.75f );
		Gizmo.Draw.LineSphere( SndPos - Transform.LocalPosition, 10.0f );
	}

	protected override void OnEnabled()
	{		
		Inner = new BBox(Transform.Position + Scale * -0.5f, Transform.Position + Scale * 0.5f );

		if ( PlayOnStart )
		{
			StartSound();
		}
	}

	TimeUntil TimeUntilRepeat;

	public override void StartSound()
	{	
		if ( StopOnNew )
		{
			SoundHandle.Stop( false );
			SoundHandle = default;
		}

		if ( SoundHandle.IsPlaying ) return;
		
		SoundHandle = Sound.Play( SoundEvent );
		SoundHandle.Position = SndPos;

		ApplyOverrides();

		TimeUntilRepeat = Random.Shared.Float( MinRepeatTime, MaxRepeatTime );
	}

	void ApplyOverrides()
	{
		if ( SoundOverride )
		{
			SoundHandle.Volume = Volume;
			SoundHandle.Pitch = Pitch;

			if ( Force2d )
				SoundHandle.Position = Sound.Listener.Position + Sound.Listener.Rotation.Forward * 10.0f;
		}
	}

	protected override void OnUpdate()
	{
		SoundHandle.Position = SndPos;

		ApplyOverrides();

		ShortestDistanceToSurface( Sound.Listener );

		if ( Repeat && TimeUntilRepeat <= 0.0f )
		{
			StartSound();
		}
	}

	public override void StopSound()
	{
		SoundHandle.Stop( false );
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		StopSound();
	}

	private void ShortestDistanceToSurface( Transform? position )
	{
		var innerclosetsPoint = Inner.ClosestPoint( position.Value.Position );

		SndPos = innerclosetsPoint;
	}
}
