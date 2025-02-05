using Sandbox.MovieMaker;
using Sandbox.Utility;

namespace Editor.MovieMaker;

#nullable enable

public enum InterpolationMode
{
	[Title( "None" )]
	None,

	[Title( "Linear" )]
	Linear,

	[Title( "Ease In" )]
	QuadraticIn,

	[Title( "Ease Out" )]
	QuadraticOut,

	[Title( "Ease In Out" )]
	QuadraticInOut
}

public static class InterpolationExtensions
{
	public static float Apply( this InterpolationMode interpolation, float t ) => interpolation switch
	{
		InterpolationMode.Linear => t,
		InterpolationMode.QuadraticIn => Easing.QuadraticIn( t ),
		InterpolationMode.QuadraticOut => Easing.QuadraticOut( t ),
		InterpolationMode.QuadraticInOut => Easing.QuadraticInOut( t ),
		_ => 0f
	};
}

public interface ILocalTransformer<T>
{
	T ToLocal( T value, T relativeTo );
	T ToGlobal( T value, T relativeTo );
}

public static class LocalTransformer
{
	public static ILocalTransformer<T>? GetDefault<T>()
	{
		// TODO: type library lookup?

		return DefaultILocalTransformer.Instance as ILocalTransformer<T>;
	}

	[SkipHotload]
	private static Dictionary<Type, ILocalTransformer<object?>?> Wrappers { get; } = new();

	[EditorEvent.Hotload]
	private static void OnHotload()
	{
		Wrappers.Clear();
	}

	public static ILocalTransformer<object?>? GetDefault( Type type )
	{
		if ( Wrappers.TryGetValue( type, out var cached ) ) return cached;

		try
		{
			var wrapperType = typeof(LocalTransformerWrapper<>).MakeGenericType( type );
			var wrapper = (ILocalTransformer<object?>)Activator.CreateInstance( wrapperType )!;

			return Wrappers[type] = wrapper;
		}
		catch
		{
			return Wrappers[type] = null;
		}
	}
}

file sealed class DefaultILocalTransformer :
	ILocalTransformer<float>, ILocalTransformer<Vector2>, ILocalTransformer<Vector3>, ILocalTransformer<Vector4>,
	ILocalTransformer<Rotation>, ILocalTransformer<Angles>,
	ILocalTransformer<Color>
{
	public static DefaultILocalTransformer Instance { get; } = new();

	public float ToLocal( float value, float relativeTo ) => value - relativeTo;
	public float ToGlobal( float value, float relativeTo ) => value + relativeTo;

	public Vector2 ToLocal( Vector2 value, Vector2 relativeTo ) => value - relativeTo;
	public Vector2 ToGlobal( Vector2 value, Vector2 relativeTo ) => value + relativeTo;

	public Vector3 ToLocal( Vector3 value, Vector3 relativeTo ) => value - relativeTo;
	public Vector3 ToGlobal( Vector3 value, Vector3 relativeTo ) => value + relativeTo;

	public Vector4 ToLocal( Vector4 value, Vector4 relativeTo ) => value - relativeTo;
	public Vector4 ToGlobal( Vector4 value, Vector4 relativeTo ) => value + relativeTo;

	public Rotation ToLocal( Rotation value, Rotation relativeTo ) => Rotation.Difference( relativeTo, value );
	public Rotation ToGlobal( Rotation value, Rotation relativeTo ) => (value * relativeTo).Normal;

	public Angles ToLocal( Angles value, Angles relativeTo ) => Rotation.Difference( relativeTo, value );
	public Angles ToGlobal( Angles value, Angles relativeTo ) => ((Rotation) value * relativeTo).Normal;

	public Color ToLocal( Color value, Color relativeTo ) => value - relativeTo;
	public Color ToGlobal( Color value, Color relativeTo ) => value + relativeTo;
}


file sealed class LocalTransformerWrapper<T> : ILocalTransformer<object?>
{
	private readonly ILocalTransformer<T> _inner;

	public LocalTransformerWrapper()
	{
		_inner = LocalTransformer.GetDefault<T>()
			?? throw new Exception( $"Can't transform type '{typeof(T)}'." );
	}

	public object? ToLocal( object? value, object? relativeTo )
	{
		return _inner.ToLocal( (T)value!, (T)relativeTo! );
	}

	public object? ToGlobal( object? value, object? relativeTo )
	{
		return _inner.ToGlobal( (T)value!, (T)relativeTo! );
	}
}
