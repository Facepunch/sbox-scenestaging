using System.Linq;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

/// <summary>
/// Centralizes the current state of a moviemaker editor session
/// </summary>
public sealed class Session
{
	public static Session? Current { get; internal set; }

	public MoviePlayer Player { get; private set; } = null!;

	internal MovieClip? Clip { get; private set; }
	internal MovieEditor Editor { get; set; } = null!;

	public bool Playing { get; set; }
	public bool Loop { get; set; } = true;
	public float TimeOffset { get; private set; }
	public float PixelsPerSecond { get; private set; } = 100.0f;

	/// <summary>
	/// When editing keyframes, what time are we changing.
	/// </summary>
	public float CurrentPointer { get; private set; }

	/// <summary>
	/// What time are we previewing (when holding shift and moving mouse over timeline).
	/// </summary>
	public float? PreviewPointer { get; private set; }

	public bool HasUnsavedChanges { get; private set; }

	public EditMode? EditMode { get; private set; }

	SmoothDeltaFloat SmoothZoom = new SmoothDeltaFloat { Value = 100.0f, Target = 100.0f, SmoothTime = 0.3f };
	SmoothDeltaFloat SmoothPan = new SmoothDeltaFloat { Value = 0.0f, Target = 0f, SmoothTime = 0.3f };

	private float? _lastPlayerPosition;

	/// <summary>
	/// Invoked when the view pans or changes scale.
	/// </summary>
	public event Action? ViewChanged;

	internal void SetPlayer( MoviePlayer player )
	{
		Player = player;
		Clip = player.MovieClip;
	}

	internal void SetEditMode( EditModeType? type )
	{
		if ( type?.IsMatchingType( EditMode ) ?? EditMode is null ) return;

		EditMode?.Disable();

		Editor.Toolbar.EditModeControls.Clear( true );

		EditMode = type?.Create();
		EditMode?.Enable( this );
	}

	public float PixelsToTime( float pixels, bool snap = false )
	{
		var t = pixels / PixelsPerSecond;

		if ( snap )
		{
			t = t.SnapToGrid( 1 / 10.0f ); // todo grid size
		}

		return t;
	}

	public float TimeToPixels( float time )
	{
		return time * PixelsPerSecond;
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

		ViewChanged?.Invoke();
	}

	public event Action<float>? PointerChanged;
	public event Action<float?>? PreviewChanged;

	public void SetCurrentPointer( float time )
	{
		CurrentPointer = Math.Max( 0, time );

		PointerChanged?.Invoke( CurrentPointer );

		Player.ApplyFrame( CurrentPointer );
	}

	public void SetPreviewPointer( float time )
	{
		PreviewPointer = Math.Max( 0, time );

		PreviewChanged?.Invoke( PreviewPointer );

		Player.ApplyFrame( PreviewPointer.Value );
	}

	public void ClearPreviewPointer()
	{
		PreviewPointer = null;

		PreviewChanged?.Invoke( PreviewPointer );

		Player.ApplyFrame( CurrentPointer );
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
		if ( !Playing && _lastPlayerPosition is { } lastPlayerPosition && !lastPlayerPosition.AlmostEqual( Player.Position ) )
		{
			CurrentPointer = lastPlayerPosition;
			PointerChanged?.Invoke( CurrentPointer );
		}

		_lastPlayerPosition = Player.Position;

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

			PixelsPerSecond = SmoothZoom.Value;
			PixelsPerSecond = PixelsPerSecond.Clamp( 5, 1024 );

			var nd = TimeToPixels( TimeOffset ) - TimeToPixels( CurrentPointer );
			ScrollBy( nd - d, false );

			ViewChanged?.Invoke();
		}

		if ( SmoothPan.Update( RealTime.Delta ) )
		{
			TimeOffset = SmoothPan.Value;
			ViewChanged?.Invoke();
		}

		return true;
	}

	internal void Zoom( float v )
	{
		SmoothZoom.Target = SmoothZoom.Target += (v * SmoothZoom.Target) * 0.01f;
		SmoothZoom.Target = SmoothZoom.Target.Clamp( 5, 1024 );
	}

	internal void ClipModified()
	{
		if ( Clip == Player.EmbeddedClip )
		{
			var property = Player.GetSerialized().GetProperty( nameof(MoviePlayer.EmbeddedClip) );

			property.SetValue( Clip );
			return;
		}

		HasUnsavedChanges = true;
	}

	public void Save()
	{
		HasUnsavedChanges = false;

		// If we're referencing a .movie resource, save it to disk

		if ( Clip != Player.ReferencedClip?.Clip )
		{
			return;
		}

		if ( AssetSystem.FindByPath( Player.ReferencedClip!.ResourcePath ) is { } asset )
		{
			asset.SaveToDisk( Player.ReferencedClip );
		}
	}
}
