using Sandbox.Utility;

namespace Editor.MovieMaker;

#nullable enable

partial class KeyframeCurve<T>
{
	protected override object OnGetValue( float time ) => GetValue( time );

	public new T GetValue( float time )
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

		// Can't interpolate? Use previous value.

		if ( _interpolator is not { } interpolator )
		{
			return prev.Value;
		}

		// Interpolate between prev.Value and next.Value

		var next = _keyframes[t1];

		var t = (time - t0) / (t1 - t0);
		var eased = (prev.Interpolation ?? Interpolation).Apply( t );

		return interpolator.Interpolate( prev.Value, next.Value, eased );
	}
}
