using Sandbox.MovieMaker.Compiled;
using Sandbox.MovieMaker;
using System.Text.Json.Serialization;

namespace Editor.MovieMaker;

#nullable enable

partial class PropertyBlock<T>
{
	public static PropertyBlock<T> Constant( T value, MovieTimeRange timeRange = default ) => new ConstantPropertyBlock<T>( value, timeRange );
}

[JsonDiscriminator( "Constant" )]
file sealed class ConstantPropertyBlock<T> : PropertyBlock<T>
{
	public T Value { get; }

	[JsonInclude]
	public new MovieTimeRange TimeRange => base.TimeRange;

	public ConstantPropertyBlock( T value, MovieTimeRange timeRange )
		: base( timeRange )
	{
		Value = value;
	}

	protected override T OnGetValue( MovieTime time ) => Value;

	public override CompiledPropertyBlock<T> Compile( ProjectTrack track ) =>
		new CompiledConstantBlock<T>( TimeRange, Value );

	protected override PropertyBlock<T> OnSlice( MovieTimeRange timeRange )
	{
		return new ConstantPropertyBlock<T>( Value, timeRange );
	}

	protected override PropertyBlock<T> OnShift( MovieTime offset )
	{
		return new ConstantPropertyBlock<T>( Value, TimeRange + offset );
	}

	protected override PropertyBlock<T> OnBlend( PropertyBlock<T> overlay, float alpha, IInterpolator<T> interpolator )
	{
		if ( overlay is not ConstantPropertyBlock<T> constantOverlay ) return base.OnBlend( overlay, alpha, interpolator );

		return new ConstantPropertyBlock<T>( interpolator.Interpolate( Value, constantOverlay.Value, alpha ), TimeRange );
	}

	protected override PropertyBlock<T>? OnTryMerge( PropertyBlock<T> next )
	{
		if ( next is not ConstantPropertyBlock<T> nextConstant ) return null;

		return EqualityComparer<T>.Default.Equals( Value, nextConstant.Value )
			? new ConstantPropertyBlock<T>( Value, TimeRange.Union( next.TimeRange ) )
			: null;
	}

	protected override int OnGetHashCode()
	{
		return HashCode.Combine( Value, TimeRange );
	}

	protected override bool EqualsBlock( PropertyBlock<T> other )
	{
		return other is ConstantPropertyBlock<T> otherConstant
			&& EqualityComparer<T>.Default.Equals( Value, otherConstant.Value );
	}
}
