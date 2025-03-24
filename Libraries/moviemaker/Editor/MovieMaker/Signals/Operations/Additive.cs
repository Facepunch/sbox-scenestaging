using System.Diagnostics.CodeAnalysis;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

partial record PropertySignal<T>
{
	[return: NotNullIfNotNull( nameof(a) )]
	public static PropertySignal<T>? operator -( PropertySignal<T>? a, PropertySignal<T> b )
	{
		if ( a is null ) return null;

		if ( LocalTransformer.GetDefault<T>() is not { } transformer ) return a;
		if ( a.Equals( b ) ) return transformer.Identity;
		if ( b.IsIdentity ) return a;

		return new ToLocalOperation<T>( a, b );
	}

	[return: NotNullIfNotNull( nameof(a) )]
	public static PropertySignal<T>? operator +( PropertySignal<T>? a, PropertySignal<T> b )
	{
		if ( a is null ) return null;

		if ( LocalTransformer.GetDefault<T>() is null ) return a;
		if ( a.IsIdentity ) return b;
		if ( b.IsIdentity ) return a;

		return new ToGlobalOperation<T>( a, b );
	}

	public virtual bool IsIdentity => false;
}

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
