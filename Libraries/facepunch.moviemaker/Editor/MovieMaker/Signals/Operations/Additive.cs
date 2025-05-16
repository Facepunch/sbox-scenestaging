using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

partial record PropertySignal<T>
{
	public static PropertySignal<T> operator -( PropertySignal<T> a, PropertySignal<T> b )
	{
		if ( Transformer.GetDefault<T>() is not { } transformer ) return a;
		if ( a.Equals( b ) ) return transformer.Identity;
		if ( b.IsIdentity ) return a;

		return new ToLocalOperation<T>( a, b );
	}

	[return: NotNullIfNotNull( nameof(a) )]
	public static PropertySignal<T> operator +( PropertySignal<T> a, PropertySignal<T> b )
	{
		if ( Transformer.GetDefault<T>() is null ) return a;
		if ( a.IsIdentity ) return b;
		if ( b.IsIdentity ) return a;

		return new ToGlobalOperation<T>( a, b );
	}

	[JsonIgnore]
	public virtual bool IsIdentity => false;
}

[JsonDiscriminator( "ToLocal" )]
file sealed record ToLocalOperation<T>( PropertySignal<T> First, PropertySignal<T> Second )
	: BinaryOperation<T>( First, Second )
{
	public override T GetValue( MovieTime time ) => _transformer.Apply(
		_transformer.Invert( Second.GetValue( time ) ),
		First.GetValue( time ) );

	private static ITransformer<T> _transformer = Transformer.GetDefaultOrThrow<T>();
}

[JsonDiscriminator( "ToGlobal" )]
file sealed record ToGlobalOperation<T>( PropertySignal<T> First, PropertySignal<T> Second )
	: BinaryOperation<T>( First, Second )
{
	public override T GetValue( MovieTime time ) => _transformer.Apply(
		First.GetValue( time ), Second.GetValue( time ) );

	protected override IEnumerable<Keyframe> OnGetKeyframes() => Second.Keyframes;

	protected override PropertySignal<T> OnWithKeyframe( MovieTime time, T value, KeyframeInterpolation interpolation )
	{
		if ( !Second.HasKeyframes ) return base.OnWithKeyframe( time, value, interpolation );

		var local = _transformer.Difference( First.GetValue( time ), value );

		return First + Second.WithKeyframe( time, local, interpolation );
	}

	protected override PropertySignal<T> OnWithKeyframeChanges( KeyframeChanges changes )
	{
		if ( !Second.HasKeyframes ) return this;

		var changed = Second.WithKeyframeChanges( changes );

		if ( !changed.HasKeyframes )
		{
			return First;
		}

		return First + changed;
	}

	private static ITransformer<T> _transformer = Transformer.GetDefaultOrThrow<T>();
}
