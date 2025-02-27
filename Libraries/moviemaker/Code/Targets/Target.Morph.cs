using System;
using static Sandbox.SkinnedModelRenderer;

namespace Sandbox.MovieMaker;

#nullable enable

partial class Target
{
	private static bool IsMorph( ITarget target, string name )
	{
		return target is IProperty<MorphAccessor?>;
	}

	private static IProperty FromMorph( ITarget target, string name )
	{
		var morphAccessorTarget = (IProperty<MorphAccessor?>)target;

		return new MorphTarget( morphAccessorTarget, name );
	}
}

file sealed class MorphTarget( IProperty<MorphAccessor?> parent, string name )
	: IProperty<float>
{
	public string Name { get; } = name;

	public Type TargetType => typeof(float);
	public ITarget Parent => parent;

	public bool IsBound => parent.Value?.Names.Contains( Name ) ?? false; // TODO: cache?
	public bool CanWrite => true;

	public float Value
	{
		get => parent.Value is { } accessor ? accessor.Get( Name ) : default!;
		set
		{
			if ( parent.Value is { } accessor )
			{
				accessor.Set( Name, value );
			}
		}
	}

	object ITarget.Value => Value;
	object? IProperty.Value
	{
		get => Value;
		set => Value = (float)value!;
	}
}
