using System;
using static Sandbox.SkinnedModelRenderer;

namespace Sandbox.MovieMaker;

#nullable enable

partial class MovieProperty
{
	private static bool IsMorph( IMovieProperty target, string name )
	{
		return target is IMember<MorphAccessor?>;
	}

	private static IMember FromMorph( IMovieProperty target, string name )
	{
		var morphAccessorTarget = (IMember<MorphAccessor?>)target;

		return new MorphMovieProperty( morphAccessorTarget, name );
	}
}

file sealed class MorphMovieProperty( IMember<MorphAccessor?> parent, string name )
	: IMember<float>
{
	public string PropertyName { get; } = name;

	public Type PropertyType => typeof(float);

	public bool IsBound => parent.Value?.Names.Contains( PropertyName ) ?? false; // TODO: cache?
	public bool CanWrite => true;

	public float Value
	{
		get => parent.Value is { } accessor ? accessor.Get( PropertyName ) : default!;
		set
		{
			if ( parent.Value is { } accessor )
			{
				accessor.Set( PropertyName, value );
			}
		}
	}

	IMovieProperty IMember.Parent => parent;

	object IMovieProperty.Value => Value;
	object? IMember.Value
	{
		get => Value;
		set => Value = (float)value!;
	}
}
