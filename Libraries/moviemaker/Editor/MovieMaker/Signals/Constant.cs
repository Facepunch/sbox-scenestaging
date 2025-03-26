using System.Collections.Immutable;
using System.Linq;
using System.Text.Json.Serialization;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

partial record PropertySignal<T>
{
	public static implicit operator PropertySignal<T>( T value ) => new ConstantSignal<T>( value );
}

[JsonDiscriminator( "Constant" )]
file sealed record ConstantSignal<T>( T Value ) : PropertySignal<T>
{
	public override T GetValue( MovieTime time ) => Value;

	public override bool IsIdentity =>
		LocalTransformer.GetDefault<T>() is { } transformer
		&& EqualityComparer<T>.Default.Equals( Value, transformer.Identity );

	protected override PropertySignal<T> OnTransform( MovieTransform value ) => this;
	protected override PropertySignal<T> OnReduce( MovieTime? start, MovieTime? end ) => this;
	protected override PropertySignal<T> OnSmooth( MovieTime size ) => this;

	public override IEnumerable<MovieTimeRange> GetPaintHints( MovieTimeRange timeRange ) => [timeRange.Start, timeRange.End - MovieTime.Epsilon];
}

partial class PropertySignalExtensions
{
	/// <summary>
	/// Creates a constant signal with the given value.
	/// </summary>
	public static PropertySignal<T> AsSignal<T>( this T value ) => value;

	public static IPropertySignal AsSignal( this object? value, Type targetType )
	{
		var signalType = typeof(ConstantSignal<>).MakeGenericType( targetType );

		return (IPropertySignal)Activator.CreateInstance( signalType, value )!;
	}

	/// <inheritdoc cref="AsSignal{T}(IReadOnlyList{PropertyBlock{T}})"/>
	public static PropertySignal<T>? AsSignal<T>( this IEnumerable<PropertyBlock<T>> blocks ) =>
		blocks.ToImmutableArray().AsSignal<T>();

	/// <summary>
	/// Creates a signal that joins together the given blocks.
	/// </summary>
	public static PropertySignal<T>? AsSignal<T>( this IReadOnlyList<PropertyBlock<T>> blocks )
	{
		if ( blocks.Count == 0 ) return null;

		// TODO: balance?

		var signal = blocks[0].Signal.Clamp( blocks[0].TimeRange );

		foreach ( var block in blocks.Skip( 1 ) )
		{
			signal = signal.HardCut( block.Signal.ClampEnd( block.TimeRange.End ), block.TimeRange.Start );
		}

		return signal;
	}
}
