using System;
using static Sandbox.SkinnedModelRenderer;

namespace Sandbox.MovieMaker;

#nullable enable

partial class MovieProperty
{
	private static bool IsAnimParam( IMovieProperty target, string name )
	{
		return target is IMovieProperty<ParameterAccessor?>;
	}

	private static IMemberMovieProperty FromAnimParam( IMovieProperty target, string name, Type? expectedType )
	{
		var paramAccessorTarget = (IMovieProperty<ParameterAccessor?>)target;

		expectedType ??= paramAccessorTarget.Value?.Graph?.GetParameterType( name ) ?? typeof(object);

		try
		{
			var propGenType = TypeLibrary.GetType( typeof( AnimParamMovieProperty<> ) );
			var propType = propGenType.MakeGenericType( [expectedType] );

			return TypeLibrary.Create<IMemberMovieProperty>( propType, [target, name] );
		}
		catch ( Exception ex )
		{
			Log.Error( ex );
			return new AnimParamMovieProperty<object>( paramAccessorTarget, name );
		}
	}
}

file sealed class AnimParamMovieProperty<T> : IMovieProperty<T>, IMemberMovieProperty
{
	public IMovieProperty<ParameterAccessor?> TargetProperty { get; }
	public IAnimParamAccessor<T> Accessor { get; }

	public AnimParamMovieProperty( IMovieProperty<ParameterAccessor?> targetProperty, string name )
	{
		TargetProperty = targetProperty;
		Accessor = DefaultAnimParamAccessor.Instance as IAnimParamAccessor<T> ?? throw new NotImplementedException();
		PropertyName = name;
	}

	public string PropertyName { get; }

	public Type PropertyType { get; } = typeof(T);

	public bool IsBound => TargetProperty.Value?.Graph?.GetParameterType( PropertyName ) == PropertyType;
	public bool CanWrite => true;

	public T Value
	{
		get => TargetProperty.Value is { } accessor ? Accessor.Get( accessor, PropertyName ) : default!;
		set
		{
			if ( TargetProperty.Value is { } accessor )
			{
				Accessor.Set( accessor, PropertyName, value );
			}
		}
	}

	IMovieProperty IMemberMovieProperty.TargetProperty => TargetProperty;

	object? IMovieProperty.Value
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
