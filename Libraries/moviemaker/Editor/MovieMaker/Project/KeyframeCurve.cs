using System.Collections;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json.Serialization;
using Sandbox.MovieMaker;
using Sandbox.MovieMaker.Compiled;
using Sandbox.UI;

namespace Editor.MovieMaker;

#nullable enable

public interface IKeyframe
{
	MovieTime Time { get; }
	object? Value { get; }
	InterpolationMode? Interpolation { get; }
}

public record struct Keyframe<T>(
	MovieTime Time, T Value,
	[property: JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	InterpolationMode? Interpolation ) : IKeyframe
{
	object? IKeyframe.Value => Value;
}

public abstract partial class KeyframeCurve : IEnumerable<IKeyframe>
{
	public InterpolationMode Interpolation { get; set; }

	public abstract Type ValueType { get; }
	public abstract int Count { get; }
	public abstract MovieTimeRange TimeRange { get; }
	public abstract bool CanInterpolate { get; }

	public static KeyframeCurve Create( Type valueType )
	{
		var typeDesc = EditorTypeLibrary.GetType( typeof(KeyframeCurve<>) ).MakeGenericType( [valueType] );

		return EditorTypeLibrary.Create<KeyframeCurve>( typeDesc );
	}

	public abstract void Clear();

	public abstract void SetKeyframe( MovieTime time, object? value, InterpolationMode? interpolation = null );
	public object GetValue( MovieTime time ) => OnGetValue( time );

	protected abstract object OnGetValue( MovieTime time );

	protected abstract IEnumerator<IKeyframe> OnGetEnumerator();

	public IEnumerator<IKeyframe> GetEnumerator() => OnGetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => OnGetEnumerator();

	public abstract IEnumerable<CompiledPropertyBlock> Compile( int sampleRate );
}

public partial class KeyframeCurve<T> : KeyframeCurve, IEnumerable<Keyframe<T>>
{
	private static IInterpolator<T>? Interpolator { get; } = Sandbox.MovieMaker.Interpolator.GetDefault<T>();

	private readonly SortedList<MovieTime, Keyframe<T>> _keyframes = new();

	public override Type ValueType => typeof( T );
	public override int Count => _keyframes.Count;

	public override MovieTimeRange TimeRange => _keyframes.Count == 0
		? default
		: new MovieTimeRange( _keyframes.Values[0].Time, _keyframes.Values[^1].Time );

	public override bool CanInterpolate => Interpolator is not null;

	public KeyframeCurve()
	{
		Interpolation = Interpolator is not null
			? InterpolationMode.QuadraticInOut
			: InterpolationMode.None;
	}

	public Keyframe<T> this[ int index ]
	{
		get => _keyframes.Values[index];
		set => _keyframes.Values[index] = value;
	}

	public override void SetKeyframe( MovieTime time, object? value, InterpolationMode? interpolation = null ) =>
		SetKeyframe( new Keyframe<T>( time, (T)value!, interpolation ) );

	public void SetKeyframe( MovieTime time, T value, InterpolationMode? interpolation = null ) =>
		SetKeyframe( new Keyframe<T>( time, value, interpolation ) );

	public void SetKeyframe( Keyframe<T> keyframe )
	{
		_keyframes[keyframe.Time] = keyframe;
	}

	public Keyframe<T> GetKeyframe( MovieTime time )
	{
		return _keyframes[time];
	}

	public void RemoveKeyframe( MovieTime time )
	{
		_keyframes.Remove( time );
	}

	public override void Clear()
	{
		_keyframes.Clear();
	}

	public override IEnumerable<CompiledPropertyBlock> Compile( int sampleRate )
	{
		if ( _keyframes.Count == 0 ) yield break;

		if ( CanInterpolate && _keyframes.Count > 1 )
		{
			// Interpolated keyframes: sample at uniform time steps

			var timeRange = TimeRange;
			var sampleCount = timeRange.Duration.GetFrameCount( sampleRate );
			var samples = new T[sampleCount];

			for ( var i = 0; i < sampleCount; ++i )
			{
				samples[i] = GetValue( MovieTime.FromFrames( i, sampleRate ) );
			}

			yield return new CompiledSampleBlock<T>( timeRange, timeRange.Start, sampleRate, [..samples] );
			yield break;
		}

		var prev = _keyframes[0];

		foreach ( var next in _keyframes.Values.Skip( 1 ) )
		{
			yield return new CompiledConstantBlock<T>( (prev.Time, next.Time), prev.Value );

			prev = next;
		}

		yield return new CompiledConstantBlock<T>( (prev.Time, prev.Time), prev.Value );
	}

	protected override IEnumerator<IKeyframe> OnGetEnumerator()
	{
		return _keyframes.Values.Cast<IKeyframe>().GetEnumerator();
	}

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	public new IEnumerator<Keyframe<T>> GetEnumerator() => _keyframes.Values.GetEnumerator();
}
