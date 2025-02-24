using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

file sealed record ShiftOperation<T>( PropertySignal<T> Signal, MovieTime Offset ) : UnaryOperation<T>( Signal )
{
	public override T GetValue( MovieTime time ) => Signal.GetValue( time + Offset );
}

partial class PropertySignalExtensions
{
	public static PropertySignal<T> Shift<T>( this PropertySignal<T> signal, MovieTime offset )
	{
		if ( offset == MovieTime.Zero )
		{
			return signal;
		}

		if ( signal is ShiftOperation<T> shift )
		{
			return shift.Signal.Shift( shift.Offset + offset );
		}

		return new ShiftOperation<T>( signal, offset );
	}
}
