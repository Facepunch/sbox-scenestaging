using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

[JsonDiscriminator( "ToLocal" )]
file sealed record ToLocalOperation<T>( PropertySignal<T> First, PropertySignal<T> Second )
	: BinaryOperation<T>( First, Second )
{
	public override T GetValue( MovieTime time ) => _transformer.ToLocal(
		First.GetValue( time ),
		Second.GetValue( time ) );

	private static ILocalTransformer<T> _transformer = LocalTransformer.GetDefaultOrThrow<T>();
}

[JsonDiscriminator( "ToGlobal" )]
file sealed record ToGlobalOperation<T>( PropertySignal<T> First, PropertySignal<T> Second )
	: BinaryOperation<T>( First, Second )
{
	public override T GetValue( MovieTime time ) => _transformer.ToGlobal(
		First.GetValue( time ),
		Second.GetValue( time ) );

	private static ILocalTransformer<T> _transformer = LocalTransformer.GetDefaultOrThrow<T>();
}


partial class PropertySignalExtensions
{
	public static PropertySignal<T> ToLocal<T>( this PropertySignal<T> first, PropertySignal<T> second )
	{
		if ( LocalTransformer.GetDefault<T>() is not { } transformer ) return first;
		if ( first.Equals( second ) ) return transformer.Identity;

		return new ToLocalOperation<T>( first, second );
	}

	public static PropertySignal<T> ToGlobal<T>( this PropertySignal<T> first, PropertySignal<T> second )
	{
		if ( LocalTransformer.GetDefault<T>() is not { } transformer ) return first;
		if ( first.IsIdentity() ) return second;

		return new ToGlobalOperation<T>( first, second );
	}

	public static bool IsIdentity<T>( this PropertySignal<T> signal )
	{
		return LocalTransformer.GetDefault<T>() is { } transformer
			&& signal is ConstantSignal<T> constant
			&& EqualityComparer<T>.Default.Equals( constant.Value, transformer.Identity );
	}
}
