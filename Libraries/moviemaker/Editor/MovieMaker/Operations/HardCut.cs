using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

[JsonDiscriminator( "HardCut" )]
file sealed record HardCutOperation<T>( PropertySignal<T> First, PropertySignal<T> Second, MovieTime Time ) : BinaryOperation<T>( First, Second )
{
	public override T GetValue( MovieTime time )
	{
		return time < Time ? First.GetValue( time ) : Second.GetValue( time );
	}
}

partial class PropertySignalExtensions
{
	public static PropertySignal<T> HardCut<T>( this PropertySignal<T> first, PropertySignal<T> second, MovieTime time )
	{
		while ( first is HardCutOperation<T> firstJoin && firstJoin.Time >= time )
		{
			first = firstJoin.First;
		}

		while ( second is HardCutOperation<T> secondJoin && secondJoin.Time <= time )
		{
			second = secondJoin.Second;
		}

		if ( second.Equals( first ) )
		{
			return first;
		}

		return new HardCutOperation<T>( first, second, time );
	}

	public static PropertySignal<T> Clamp<T>( this PropertySignal<T> signal, MovieTimeRange timeRange ) =>
		signal.ClampStart( timeRange.Start ).ClampEnd( timeRange.End );

	public static PropertySignal<T> ClampStart<T>( this PropertySignal<T> signal, MovieTime time ) =>
		signal.GetValue( time ).AsSignal().HardCut( signal, time );

	public static PropertySignal<T> ClampEnd<T>( this PropertySignal<T> signal, MovieTime time ) =>
		signal.HardCut( signal.GetValue( time ), time );
}
