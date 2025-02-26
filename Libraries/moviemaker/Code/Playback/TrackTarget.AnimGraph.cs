using System;
using static Sandbox.SkinnedModelRenderer;

namespace Sandbox.MovieMaker;

#nullable enable

partial class TrackTarget
{
	private static bool IsAnimParam( ITrackTarget target, string name )
	{
		return target is IMember<ParameterAccessor?>;
	}

	private static IMember FromAnimParam( ITrackTarget target, string name, Type? expectedType )
	{
		var paramAccessorTarget = (IMember<ParameterAccessor?>)target;

		expectedType ??= paramAccessorTarget.Value?.Graph?.GetParameterType( name ) ?? typeof(object);

		try
		{
			var propGenType = TypeLibrary.GetType( typeof( AnimParamTarget<> ) );
			var propType = propGenType.MakeGenericType( [expectedType] );

			return TypeLibrary.Create<IMember>( propType, [target, name] );
		}
		catch ( Exception ex )
		{
			Log.Error( ex );
			return new AnimParamTarget<object>( paramAccessorTarget, name );
		}
	}
}

file sealed class AnimParamTarget<T>( IMember<ParameterAccessor?> parent, string name )
	: IMember<T>
{
	private IAnimParamAccessor<T> Accessor { get; } = DefaultAnimParamAccessor.Instance as IAnimParamAccessor<T> ?? throw new NotImplementedException();

	public string Name { get; } = name;

	public Type TargetType { get; } = typeof(T);
	public ITrackTarget Parent => parent;

	public bool IsBound => parent.Value?.Graph?.GetParameterType( Name ) == TargetType;
	public bool CanWrite => true;

	public T Value
	{
		get => parent.Value is { } accessor ? Accessor.Get( accessor, Name ) : default!;
		set
		{
			if ( parent.Value is { } accessor )
			{
				Accessor.Set( accessor, Name, value );
			}
		}
	}

	object? ITrackTarget.Value => Value;

	object? IMember.Value
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
