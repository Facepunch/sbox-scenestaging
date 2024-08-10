using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

/// <summary>
/// Centralizes the current state of a moviemaker editor session
/// </summary>
public sealed class Session
{
	public static Session Current { get; internal set; }

	public MovieClip Clip { get; private set; }

	/// <summary>
	/// If true, we automatically record new keyframes when properties are changed
	/// </summary>
	public bool KeyframeRecording { get; set; }


	public bool Playing { get; set; }
	public bool Loop { get; set; } = true;
	public float TimeOffset = 0;
	public float TimeVisible = 100.0f;
	public float CurrentPointer { get; private set; }

	SmoothDeltaFloat SmoothZoom = new SmoothDeltaFloat { Value = 100.0f, Target = 100.0f, SmoothTime = 0.3f };
	SmoothDeltaFloat SmoothPan = new SmoothDeltaFloat { Value = 0.0f, Target = 0f, SmoothTime = 0.3f };

	internal void SetClip( MovieClip clip )
	{
		Clip = clip;
	}

	public float PixelsToTime( float pixels, bool snap = false )
	{
		var t = pixels / TimeVisible;

		if ( snap )
		{
			t = t.SnapToGrid( 1 / 10.0f ); // todo grid size
		}

		return t;
	}

	public float TimeToPixels( float time )
	{
		return time * TimeVisible;
	}

	public void ScrollBy( float x, bool smooth )
	{
		if ( x == 0 )
			return;

		var time = PixelsToTime( x );

		SmoothPan.Target -= time;
		if ( SmoothPan.Target < 0 ) SmoothPan.Target = 0;

		if ( !smooth )
		{
			SmoothPan.Value = SmoothPan.Target;
			SmoothPan.Velocity = 0;
			TimeOffset = SmoothPan.Target;
		}
	}

	public Action<float> OnPointerChanged;

	public void SetCurrentPointer( float time )
	{
		// gonna be using Json.* to lookup - so needs the session
		using ( SceneEditorSession.Active.Scene.Push() )
		{
			CurrentPointer = time;

			if ( CurrentPointer < 0 )
				CurrentPointer = 0;

			OnPointerChanged?.Invoke( CurrentPointer );

			Clip?.ScrubTo( CurrentPointer );
		}
	}

	public void Play()
	{
		if ( Playing ) return;

		Playing = true;
	}

	public void Stop()
	{
		if ( !Playing ) return;

		Playing = false;
	}

	public bool Frame()
	{
		if ( Playing )
		{
			var targetTime = CurrentPointer + RealTime.Delta;

			// got to the end
			if ( targetTime >= Clip.Duration && Clip.Duration > 0 )
			{
				if ( Loop )
				{
					targetTime = 0;
				}
				else
				{
					targetTime = Clip.Duration;

					Playing = false;
				}
			}

			SetCurrentPointer( targetTime );
		}

		if ( SmoothZoom.Update( RealTime.Delta ) )
		{
			var d = TimeToPixels( TimeOffset ) - TimeToPixels( CurrentPointer );

			TimeVisible = SmoothZoom.Value;
			TimeVisible = TimeVisible.Clamp( 5, 1024 );

			var nd = TimeToPixels( TimeOffset ) - TimeToPixels( CurrentPointer );
			ScrollBy( nd - d, false );
		}

		if ( SmoothPan.Update( RealTime.Delta ) )
		{
			TimeOffset = SmoothPan.Value;
		}

		return true;
	}

	internal void Zoom( float v )
	{
		SmoothZoom.Target = SmoothZoom.Target += (v * SmoothZoom.Target) * 0.01f;
		SmoothZoom.Target = SmoothZoom.Target.Clamp( 5, 1024 );
	}
}

