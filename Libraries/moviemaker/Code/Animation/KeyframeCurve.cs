using System;

namespace Sandbox.Animation;

#nullable enable

public record struct Keyframe<T>( T Value, KeyframeEasing Easing );

public enum KeyframeEasing
{
	None,
	Linear
}

public abstract class KeyframeCurve
{
	public abstract Type ValueType { get; }
	public abstract IEnumerable<float> Keys { get; }

	public static KeyframeCurve Create( Type valueType )
	{
		var typeDesc = TypeLibrary.GetType( typeof(KeyframeCurve<>) ).MakeGenericType( [valueType] );

		return TypeLibrary.Create<KeyframeCurve>( typeDesc );
	}
}

public class KeyframeCurve<T> : KeyframeCurve
{
	private readonly SortedList<float, Keyframe<T>> _keyframes = new();
	private readonly IInterpolator<T>? _interpolator = GetDefaultInterpolator();

	public override Type ValueType => typeof( T );
	public override IEnumerable<float> Keys => _keyframes.Keys;

	private static IInterpolator<T>? GetDefaultInterpolator()
	{
		// TODO: look up in TypeLibrary?

		return DefaultInterpolator.Instance as IInterpolator<T>;
	}

	public void SetKeyframe( float time, T value, KeyframeEasing easing = KeyframeEasing.Linear ) =>
		SetKeyframe( time, new Keyframe<T>( value, easing ) );

	public void SetKeyframe( float time, Keyframe<T> value )
	{
		_keyframes[time] = value;
	}

	public Keyframe<T> GetKeyframe( float time )
	{
		return _keyframes[time];
	}

	public void RemoveKeyframe( float time )
	{
		_keyframes.Remove( time );
	}

	public T GetValue( float time )
	{
		if ( _keyframes.Count == 0 ) return default!;

		var (prevTime, nextTime) = _keyframes.GetNeighborKeys( time );

		// Clamp to start / end

		if ( prevTime is not { } t0 )
		{
			return _keyframes.Values[0].Value;
		}

		if ( nextTime is not { } t1 )
		{
			return _keyframes.Values[^1].Value;
		}

		// We're between two keyframes

		var prev = _keyframes[t0];
		var next = _keyframes[t1];

		// Can't interpolate? Use previous value.

		if ( _interpolator is not { } interpolator )
		{
			return prev.Value;
		}

		// Interpolate between prev.Value and next.Value

		var t = (time - t0) / (t1 - t0);
		var eased = prev.Easing.Apply( t );

		return interpolator.Interpolate( prev.Value, next.Value, eased );
	}
}
