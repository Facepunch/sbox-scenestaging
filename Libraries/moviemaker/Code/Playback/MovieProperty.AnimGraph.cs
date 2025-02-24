using System;
using static Sandbox.SkinnedModelRenderer;

namespace Sandbox.MovieMaker;

#nullable enable

partial class MovieProperty
{
	private static bool IsAnimParam( IMovieProperty target, string name )
	{
		return target is IMemberProperty<ParameterAccessor?>;
	}

	private static IMemberProperty FromAnimParam( IMovieProperty target, string name, Type? expectedType )
	{
		var paramAccessorTarget = (IMemberProperty<ParameterAccessor?>)target;

		expectedType ??= paramAccessorTarget.Value?.Graph?.GetParameterType( name ) ?? typeof(object);

		try
		{
			var propGenType = TypeLibrary.GetType( typeof( AnimParamMovieProperty<> ) );
			var propType = propGenType.MakeGenericType( [expectedType] );

			return TypeLibrary.Create<IMemberProperty>( propType, [target, name] );
		}
		catch ( Exception ex )
		{
			Log.Error( ex );
			return new AnimParamMovieProperty<object>( paramAccessorTarget, name );
		}
	}
}

file sealed class AnimParamMovieProperty<T>( IMemberProperty<ParameterAccessor?> parent, string name )
	: IMemberProperty<T>
{
	private IAnimParamAccessor<T> Accessor { get; } = DefaultAnimParamAccessor.Instance as IAnimParamAccessor<T> ?? throw new NotImplementedException();

	public string PropertyName { get; } = name;

	public Type PropertyType { get; } = typeof(T);

	public bool IsBound => parent.Value?.Graph?.GetParameterType( PropertyName ) == PropertyType;
	public bool CanWrite => true;

	public T Value
	{
		get => parent.Value is { } accessor ? Accessor.Get( accessor, PropertyName ) : default!;
		set
		{
			if ( parent.Value is { } accessor )
			{
				Accessor.Set( accessor, PropertyName, value );
			}
		}
	}

	IMovieProperty IMemberProperty.Parent => parent;

	object? IMovieProperty.Value => Value;

	object? IMemberProperty.Value
	{
		get => Value;
		set => Value = (T)value!;
	}
}

file interface IAnimParamAccessor<T>
{
	T Get( ParameterAccessor accessor, string name );
	void Set( ParameterAccessor accessor, string name, T value );
}

file sealed class DefaultAnimParamAccessor :
	IAnimParamAccessor<bool>, IAnimParamAccessor<byte>, IAnimParamAccessor<int>, IAnimParamAccessor<float>,
	IAnimParamAccessor<Vector3>, IAnimParamAccessor<Rotation>
{
	public static DefaultAnimParamAccessor Instance { get; } = new();

	bool IAnimParamAccessor<bool>.Get( ParameterAccessor accessor, string name ) => accessor.GetBool( name );
	byte IAnimParamAccessor<byte>.Get( ParameterAccessor accessor, string name ) => (byte)accessor.GetInt( name );
	int IAnimParamAccessor<int>.Get( ParameterAccessor accessor, string name ) => accessor.GetInt( name );
	float IAnimParamAccessor<float>.Get( ParameterAccessor accessor, string name ) => accessor.GetFloat( name );
	Vector3 IAnimParamAccessor<Vector3>.Get( ParameterAccessor accessor, string name ) => accessor.GetVector( name );
	Rotation IAnimParamAccessor<Rotation>.Get( ParameterAccessor accessor, string name ) => accessor.GetRotation( name );

	void IAnimParamAccessor<bool>.Set( ParameterAccessor accessor, string name, bool value ) => accessor.Set( name, value );
	void IAnimParamAccessor<byte>.Set( ParameterAccessor accessor, string name, byte value ) => accessor.Set( name, value );
	void IAnimParamAccessor<int>.Set( ParameterAccessor accessor, string name, int value ) => accessor.Set( name, value );
	void IAnimParamAccessor<float>.Set( ParameterAccessor accessor, string name, float value ) => accessor.Set( name, value );
	void IAnimParamAccessor<Vector3>.Set( ParameterAccessor accessor, string name, Vector3 value ) => accessor.Set( name, value );
	void IAnimParamAccessor<Rotation>.Set( ParameterAccessor accessor, string name, Rotation value ) => accessor.Set( name, value );
}
