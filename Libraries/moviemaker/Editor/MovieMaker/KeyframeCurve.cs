using System.Collections;
using System.Linq;
using System.Text.Json.Serialization;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

public interface IKeyframe
{
	float Time { get; }
	object? Value { get; }
	InterpolationMode? Interpolation { get; }
}

public record struct Keyframe<T>(
	float Time, T Value,
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
	public abstract float StartTime { get; }
	public abstract float Duration { get; }
	public abstract bool CanInterpolate { get; }

	public static KeyframeCurve Create( Type valueType )
	{
		var typeDesc = EditorTypeLibrary.GetType( typeof(KeyframeCurve<>) ).MakeGenericType( [valueType] );

		return EditorTypeLibrary.Create<KeyframeCurve>( typeDesc );
	}

	public abstract void Clear();

	public abstract void SetKeyframe( float time, object? value, InterpolationMode? interpolation = null );
	public object GetValue( float time ) => OnGetValue( time );

	protected abstract object OnGetValue( float time );

	protected abstract IEnumerator<IKeyframe> OnGetEnumerator();

	public IEnumerator<IKeyframe> GetEnumerator() => OnGetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => OnGetEnumerator();
}

public partial class KeyframeCurve<T> : KeyframeCurve, IEnumerable<Keyframe<T>>
{
	private readonly SortedList<float, Keyframe<T>> _keyframes = new();
	private readonly IInterpolator<T>? _interpolator = Interpolator.GetDefault<T>();

	public override Type ValueType => typeof( T );
	public override int Count => _keyframes.Count;

	public override float StartTime => _keyframes.Count == 0 ? 0f : _keyframes.Values[0].Time;
	public override float Duration => _keyframes.Count == 0 ? 0f : _keyframes.Values[^1].Time - _keyframes.Values[0].Time;

	public override bool CanInterpolate => _interpolator is not null;

	public KeyframeCurve()
	{
		Interpolation = _interpolator is not null
			? InterpolationMode.QuadraticInOut
			: InterpolationMode.None;
	}

	public Keyframe<T> this[ int index ]
	{
		get => _keyframes.Values[index];
		set => _keyframes.Values[index] = value;
	}

	public override void SetKeyframe( float time, object? value, InterpolationMode? interpolation = null ) =>
		SetKeyframe( new Keyframe<T>( time, (T)value!, interpolation ) );

	public void SetKeyframe( float time, T value, InterpolationMode? interpolation = null ) =>
		SetKeyframe( new Keyframe<T>( time, value, interpolation ) );

	public void SetKeyframe( Keyframe<T> keyframe )
	{
		_keyframes[keyframe.Time] = keyframe;
	}

	public Keyframe<T> GetKeyframe( float time )
	{
		return _keyframes[time];
	}

	public void RemoveKeyframe( float time )
	{
		_keyframes.Remove( time );
	}

	public override void Clear()
	{
		_keyframes.Clear();
	}

	protected override IEnumerator<IKeyframe> OnGetEnumerator()
	{
		return _keyframes.Values.Cast<IKeyframe>().GetEnumerator();
	}

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	public new IEnumerator<Keyframe<T>> GetEnumerator() => _keyframes.Values.GetEnumerator();
}
