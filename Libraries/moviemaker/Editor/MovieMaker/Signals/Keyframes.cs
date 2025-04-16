using System.Collections.Immutable;
using System.Linq;
using System.Text.Json.Serialization;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

public enum KeyframeInterpolation
{
	Unknown = -1,

	Linear = 0,
	Quadratic,
	Cubic
}

public readonly record struct Keyframe( MovieTime Time, object? Value, KeyframeInterpolation Interpolation ) : IComparable<Keyframe>
{
	public int CompareTo( Keyframe other ) => Time.CompareTo( other.Time );

	public static InterpolationMode GetInterpolationMode( KeyframeInterpolation prev, KeyframeInterpolation next ) => (prev, next) switch
	{
		(KeyframeInterpolation.Linear, KeyframeInterpolation.Linear) => InterpolationMode.Linear,
		(_, KeyframeInterpolation.Linear) => InterpolationMode.QuadraticIn,
		(KeyframeInterpolation.Linear, _) => InterpolationMode.QuadraticOut,
		_ => InterpolationMode.QuadraticInOut
	};
}

partial record PropertySignal
{
	private ImmutableArray<Keyframe>? _keyframes;

	[JsonIgnore]
	public IReadOnlyList<Keyframe> Keyframes => _keyframes ??= [..OnGetKeyframes().Order()];

	[JsonIgnore]
	public bool HasKeyframes => Keyframes.Count > 0;

	public IEnumerable<Keyframe> GetKeyframes( MovieTimeRange timeRange ) => Keyframes
		.SkipWhile( x => x.Time < timeRange.Start )
		.TakeWhile( x => x.Time <= timeRange.End );

	protected virtual IEnumerable<Keyframe> OnGetKeyframes() => [];
}

partial record PropertySignal<T>
{
	public PropertySignal<T> WithKeyframe( MovieTime time, T value, KeyframeInterpolation interpolation ) => OnWithKeyframe( time, value, interpolation );
	public PropertySignal<T> WithKeyframeChanges( KeyframeChanges changes ) =>
		HasKeyframes ? OnWithKeyframeChanges( changes ) : this;

	protected virtual PropertySignal<T> OnWithKeyframe( MovieTime time, T value, KeyframeInterpolation interpolation )
	{
		if ( Transformer.GetDefault<T>() is not { } transformer )
		{
			// If we can't do additive blending, replace this signal with the new keyframe signal.

			return new KeyframeSignal<T>( [new Keyframe<T>( time, value, interpolation )] );
		}

		var relative = transformer.Difference( GetValue( time ), value );

		return this + new KeyframeSignal<T>( [new Keyframe<T>( time, relative, interpolation )] );
	}

	protected virtual PropertySignal<T> OnWithKeyframeChanges( KeyframeChanges changes ) => this;
}

partial interface IProjectPropertyTrack
{
	bool AddKeyframe( MovieTime time, object? value, KeyframeInterpolation interpolation );
}

partial class ProjectPropertyTrack<T>
{
	public bool AddKeyframe( MovieTime time, T value, KeyframeInterpolation interpolation )
	{
		time = MovieTime.Max( 0d, time );

		if ( Blocks.Count == 0 )
		{
			return Add( time, new KeyframeSignal<T>( [new Keyframe<T>( time, value, interpolation )] ) );
		}

		var block = Blocks.GetLastBlock( time );

		return Add( block.TimeRange.Union( time ), block.Signal.WithKeyframe( time, value, interpolation ) );
	}

	bool IProjectPropertyTrack.AddKeyframe( MovieTime time, object? value, KeyframeInterpolation interpolation ) =>
		AddKeyframe( time, (T)value!, interpolation );
}

public abstract record KeyframeChanges( ImmutableHashSet<MovieTime> KeyframeTimes )
{
	public abstract string Name { get; }

	public virtual MovieTime? Apply( MovieTime time ) => time;

	public virtual IEnumerable<MovieTime> Apply( IEnumerable<MovieTime> keyframeTimes ) => keyframeTimes;
}

public sealed record KeyframeTransform( ImmutableHashSet<MovieTime> KeyframeTimes, MovieTransform Transform )
	: KeyframeChanges( KeyframeTimes )
{
	public override string Name => "Move";

	public override MovieTime? Apply( MovieTime time ) => KeyframeTimes.Contains( time ) ? Transform * time : time;

	public override IEnumerable<MovieTime> Apply( IEnumerable<MovieTime> keyframeTimes ) => keyframeTimes
		.Select( x => KeyframeTimes.Contains( x ) ? Transform * x : x )
		.Order()
		.Distinct();
}

public sealed record KeyframeDeletion( ImmutableHashSet<MovieTime> KeyframeTimes )
	: KeyframeChanges( KeyframeTimes )
{
	public override string Name => "Delete";

	public override MovieTime? Apply( MovieTime time ) => KeyframeTimes.Contains( time ) ? null : time;

	public override IEnumerable<MovieTime> Apply( IEnumerable<MovieTime> keyframeTimes ) => keyframeTimes
		.Except( KeyframeTimes );
}

public sealed record KeyframeSetInterpolation(
	ImmutableHashSet<MovieTime> KeyframeTimes,
	KeyframeInterpolation Interpolation )
	: KeyframeChanges( KeyframeTimes )
{
	public override string Name => "Modify";
}

public readonly record struct Keyframe<T>(
	MovieTime Time,
	T Value,
	KeyframeInterpolation Interpolation ) : IComparable<Keyframe<T>>
{
	public static implicit operator Keyframe( Keyframe<T> keyframe ) =>
		new ( keyframe.Time, keyframe.Value, keyframe.Interpolation );

	public int CompareTo( Keyframe<T> other ) => Time.CompareTo( other.Time );
}

[JsonDiscriminator( "Keyframes" )]
file sealed record KeyframeSignal<T>( ImmutableArray<Keyframe<T>> Keyframes ) : PropertySignal<T>
{
	private readonly ImmutableArray<Keyframe<T>> _keyframes = ValidateKeyframes( Keyframes );

	public ImmutableArray<Keyframe<T>> Keyframes
	{
		get => _keyframes;
		init => _keyframes = ValidateKeyframes( value );
	}

	[JsonIgnore]
	public MovieTimeRange TimeRange => (Keyframes[0].Time, Keyframes[^1].Time);

	protected override IEnumerable<Keyframe> OnGetKeyframes() => Keyframes.Select( x => (Keyframe)x );

	public override T GetValue( MovieTime time )
	{
		if ( time <= Keyframes[0].Time )
		{
			return Keyframes[0].Value;
		}

		if ( time >= Keyframes[^1].Time )
		{
			return Keyframes[^1].Value;
		}

		var index = FindIndex( time );

		if ( _interpolator is not { } interpolator )
		{
			return Keyframes[index].Value;
		}

		var p0 = Keyframes[index];
		var p1 = Keyframes[index + 1];

		var timeRange = new MovieTimeRange( p0.Time, p1.Time );
		var fraction = timeRange.GetFraction( time );

		if ( fraction <= 0f ) return p0.Value;
		if ( fraction >= 1f ) return p1.Value;

		if ( _transformer is not { } transformer || p0.Interpolation <= KeyframeInterpolation.Quadratic && p1.Interpolation <= KeyframeInterpolation.Quadratic )
		{
			var mode = Keyframe.GetInterpolationMode( p0.Interpolation, p1.Interpolation );
			return interpolator.Interpolate( p0.Value, p1.Value, mode.Apply( fraction ) );
		}

		var pPrev = Keyframes[Math.Max( 0, index - 1 )];
		var pNext = Keyframes[Math.Min( Keyframes.Length - 1, index + 2 )];

		var tangent0 = transformer.Difference( pPrev.Value, p1.Value );
		var tangent1 = transformer.Difference( pNext.Value, p0.Value );

		var control0 = p0.Interpolation <= KeyframeInterpolation.Quadratic ? p0.Value : interpolator.Interpolate( p0.Value, transformer.Apply( p0.Value, tangent0 ), 1f / 6f );
		var control1 = p1.Interpolation <= KeyframeInterpolation.Quadratic ? p1.Value : interpolator.Interpolate( p1.Value, transformer.Apply( p1.Value, tangent1 ), 1f / 6f );

		// De Casteljau's algorithm

		var a0 = interpolator.Interpolate( p0.Value, control0, fraction );
		var a1 = interpolator.Interpolate( control0, control1, fraction );
		var a2 = interpolator.Interpolate( control1, p1.Value, fraction );

		var b0 = p0.Interpolation == KeyframeInterpolation.Linear ? p0.Value : interpolator.Interpolate( a0, a1, fraction );
		var b1 = p1.Interpolation == KeyframeInterpolation.Linear ? p1.Value : interpolator.Interpolate( a1, a2, fraction );

		return interpolator.Interpolate( b0, b1, fraction );
	}

	/// <summary>
	/// Get the current keyframe index for the given time.
	/// </summary>
	private int FindIndex( MovieTime time )
	{
		var first = Keyframes[0];
		var last = Keyframes[^1];

		if ( time < first.Time ) return 0;
		if ( time >= last.Time ) return Keyframes.Length - 1;

		// TODO: binary search?

		for ( var i = 1; i < Keyframes.Length; ++i )
		{
			var next = Keyframes[i];

			if ( next.Time > time ) return i - 1;
		}

		throw new Exception( "Should have already returned!" );
	}

	private int? FindIndexExact( MovieTime time )
	{
		// TODO: binary search?

		for ( var i = 0; i < Keyframes.Length; ++i )
		{
			var keyframe = Keyframes[i];

			if ( keyframe.Time < time ) continue;
			if ( keyframe.Time > time ) break;

			return i;
		}

		return null;
	}

	protected override PropertySignal<T> OnWithKeyframe( MovieTime time, T value, KeyframeInterpolation interpolation )
	{
		var keyframe = new Keyframe<T>( time, value, interpolation );

		if ( time < Keyframes[0].Time )
		{
			return this with { Keyframes = [keyframe, ..Keyframes] };
		}

		if ( time > Keyframes[^1].Time )
		{
			return this with { Keyframes = [..Keyframes, keyframe] };
		}

		if ( FindIndexExact( time ) is { } index )
		{
			return this with { Keyframes = [..Keyframes[..index], keyframe, ..Keyframes[(index + 1)..]] };
		}

		var prevIndex = FindIndex( time );

		return this with { Keyframes = [..Keyframes[..(prevIndex + 1)], keyframe, ..Keyframes[(prevIndex + 1)..]] };
	}

	protected override PropertySignal<T> OnWithKeyframeChanges( KeyframeChanges changes )
	{
		switch ( changes )
		{
			case KeyframeDeletion:
				if ( Keyframes.All( x => changes.KeyframeTimes.Contains( x.Time ) ) )
				{
					// All keyframes deleted: return a constant signal of the first keyframe's value

					return Keyframes[0].Value;
				}

				return this with { Keyframes = [..Keyframes.Where( x => !changes.KeyframeTimes.Contains( x.Time ) )] };

			case KeyframeTransform { Transform: var transform }:
				return this with
				{
					Keyframes = [..Keyframes
						.Select( x => changes.KeyframeTimes.Contains( x.Time )
							? x with { Time = transform * x.Time }
							: x )
						
						// If keyframes start overlapping, we only care about the first and last in each overlap
						.GroupBy( x => x.Time )
						.SelectMany( x => x.Count() < 2 ? x.AsEnumerable() : [x.First(), x.Last()] )
						.Order()
					]
				};

			case KeyframeSetInterpolation { Interpolation: var interpolation }:
				return this with
				{
					Keyframes =
					[
						..Keyframes.Select( x => changes.KeyframeTimes.Contains( x.Time )
							? x with { Interpolation = interpolation } : x )
					]
				};

			default:
				throw new NotImplementedException();
		}
	}

	protected override PropertySignal<T> OnReduce( MovieTime? start, MovieTime? end )
	{
		var i = 0;
		var j = Keyframes.Length - 1;

		if ( start is { } s )
		{
			i = FindIndex( s );
		}

		if ( end is { } e )
		{
			j = Math.Min( FindIndex( e ) + 1, Keyframes.Length - 1);
		}

		if ( i == 0 && j == Keyframes.Length - 1 ) return this;

		return this with { Keyframes = Keyframes[i..(j + 1)] };
	}

	protected override PropertySignal<T> OnTransform( MovieTransform value ) =>
		new KeyframeSignal<T>( [..Keyframes.Select( x => x with { Time = value * x.Time } )] );

	public override IEnumerable<MovieTimeRange> GetPaintHints( MovieTimeRange timeRange )
	{
		var prev = Keyframes[0];

		// TODO: non-interpolated

		foreach ( var next in Keyframes.Skip( 1 ) )
		{
			if ( timeRange.Intersect( (prev.Time, next.Time) ) is { } intersection )
			{
				yield return intersection;
			}

			prev = next;
		}
	}

	private static ImmutableArray<Keyframe<T>> ValidateKeyframes( ImmutableArray<Keyframe<T>> keyframes )
	{
		if ( keyframes.IsDefaultOrEmpty )
		{
			throw new ArgumentException( "Expected at least one keyframe.", nameof(keyframes) );
		}

		var prevTime = keyframes[0].Time;

		foreach ( var keyframe in keyframes.Skip( 1 ) )
		{
			if ( keyframe.Time < prevTime )
			{
				throw new ArgumentException( "Keyframes must be sorted by ascending time.", nameof(keyframes) );
			}

			prevTime = keyframe.Time;
		}

		return keyframes;
	}

	private static readonly IInterpolator<T>? _interpolator = Interpolator.GetDefault<T>();
	private static readonly ITransformer<T>? _transformer = Transformer.GetDefault<T>();
}
