using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

public abstract record InterpolateOperation<T>( PropertySignal<T> First, PropertySignal<T> Second ) : BinaryOperation<T>( First, Second )
{
	private static readonly IInterpolator<T> _interpolator = Interpolator.GetDefaultOrThrow<T>();

	public abstract float GetAlpha( MovieTime time );

	public override T GetValue( MovieTime time ) =>
		_interpolator.Interpolate( First.GetValue( time ), Second.GetValue( time ), GetAlpha( time ) );
}
