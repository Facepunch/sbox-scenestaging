using System;
using static Sandbox.SkinnedModelRenderer;

namespace Sandbox.MovieMaker;

#nullable enable

partial class TrackTarget
{
	private static bool IsMorph( ITrackTarget target, string name )
	{
		return target is IMember<MorphAccessor?>;
	}

	private static IMember FromMorph( ITrackTarget target, string name )
	{
		var morphAccessorTarget = (IMember<MorphAccessor?>)target;

		return new MorphTarget( morphAccessorTarget, name );
	}
}

file sealed class MorphTarget( IMember<MorphAccessor?> parent, string name )
	: IMember<float>
{
	public string Name { get; } = name;

	public Type TargetType => typeof(float);
	public ITrackTarget Parent => parent;

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

	object ITrackTarget.Value => Value;
	object? IMember.Value
	{
		get => Value;
		set => Value = (float)value!;
	}
}
