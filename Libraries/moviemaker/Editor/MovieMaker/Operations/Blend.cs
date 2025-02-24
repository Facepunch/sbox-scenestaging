using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

[JsonDiscriminator( "Blend" )]
file sealed record BlendOperation<T>( PropertySignal<T> First, PropertySignal<T> Second, float Alpha ) : InterpolateOperation<T>( First, Second )
{
	public override float GetAlpha( MovieTime time ) => Alpha;
}

partial class PropertySignalExtensions
{
	public static PropertySignal<T> Blend<T>( this PropertySignal<T> first, PropertySignal<T> second, float alpha )
	{
		if ( alpha <= 0f ) return first;
		if ( alpha >= 1f ) return second;

		if ( first.Equals( second ) )
		{
			return first;
		}

		if ( Interpolator.GetDefault<T>() is null )
		{
			return first;
		}

		return new BlendOperation<T>( first, second, alpha );
	}
}
