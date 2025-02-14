using System;
using static Sandbox.SkinnedModelRenderer;

namespace Sandbox.MovieMaker;

#nullable enable

partial class MovieProperty
{
	private static bool IsMorph( IMovieProperty target, string name )
	{
		return target is IMovieProperty<MorphAccessor?>;
	}

	private static IMemberMovieProperty FromMorph( IMovieProperty target, string name )
	{
		var morphAccessorTarget = (IMovieProperty<MorphAccessor?>)target;

		return new MorphMovieProperty( morphAccessorTarget, name );
	}
}

file sealed class MorphMovieProperty : IMovieProperty<float>, IMemberMovieProperty
{
	public IMovieProperty<MorphAccessor?> TargetProperty { get; }

	public MorphMovieProperty( IMovieProperty<MorphAccessor?> targetProperty, string name )
	{
		TargetProperty = targetProperty;
		PropertyName = name;
	}

	public string PropertyName { get; }

	public Type PropertyType => typeof(float);

	public bool IsBound => TargetProperty.Value?.Names.Contains( PropertyName ) ?? false; // TODO: cache?

	public float Value
	{
		get => TargetProperty.Value is { } accessor ? accessor.Get( PropertyName ) : default!;
		set
		{
			if ( TargetProperty.Value is { } accessor )
			{
				accessor.Set( PropertyName, value );
			}
		}
	}

	IMovieProperty IMemberMovieProperty.TargetProperty => TargetProperty;

	object? IMovieProperty.Value
	{
		get => Value;
		set => Value = (float)value!;
	}
}
