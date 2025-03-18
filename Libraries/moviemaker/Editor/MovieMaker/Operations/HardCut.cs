using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

[JsonDiscriminator( "HardCut" )]
file sealed record HardCutOperation<T>( PropertySignal<T> First, PropertySignal<T> Second, MovieTime Time )
	: BinaryOperation<T>( First, Second )
{
	public override T GetValue( MovieTime time )
	{
		return time < Time ? First.GetValue( time ) : Second.GetValue( time );
	}

	protected override PropertySignal<T> OnTransform( MovieTime offset ) => this with
	{
		First = First.Transform( offset ),
		Second = Second.Transform( offset ),
		Time = Time + offset
	};

	protected override PropertySignal<T> OnReduce( MovieTime? start, MovieTime? end )
	{
		return !TryReduceTransition( start, end, Time, out var reduced,
			out var before, out var after )
			? before.HardCut( after, Time )
			: reduced;
	}

	public override IEnumerable<MovieTimeRange> GetPaintHints( MovieTimeRange timeRange )
	{
		return GetTransitionPaintHints( timeRange, Time );
	}
}

partial class PropertySignalExtensions
{
	public static PropertySignal<T> HardCut<T>( this PropertySignal<T> first, PropertySignal<T> second, MovieTime time )
	{
		return !second.Equals( first )
			? new HardCutOperation<T>( first, second, time )
			: first;
	}

	public static PropertySignal<T> Clamp<T>( this PropertySignal<T> signal, MovieTimeRange timeRange ) =>
		signal.ClampStart( timeRange.Start ).ClampEnd( timeRange.End );

	public static PropertySignal<T> ClampStart<T>( this PropertySignal<T> signal, MovieTime time ) =>
		signal.GetValue( time ).AsSignal().HardCut( signal, time );

	public static PropertySignal<T> ClampEnd<T>( this PropertySignal<T> signal, MovieTime time ) =>
		signal.HardCut( signal.GetValue( time ), time );

	public static IEnumerable<PropertyBlock<T>> AsBlocks<T>( this PropertySignal<T> signal, MovieTimeRange timeRange )
	{
		signal = signal.Reduce( timeRange );

		if ( signal is not HardCutOperation<T> hardCut ) return [new PropertyBlock<T>( signal, timeRange )];

		return
		[
			..hardCut.First.AsBlocks( timeRange with { End = hardCut.Time } ),
			..hardCut.Second.AsBlocks( timeRange with { Start = hardCut.Time } )
		];
	}
}
