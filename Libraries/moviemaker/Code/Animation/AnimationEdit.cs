namespace Sandbox.Animation;

#nullable enable

internal interface IAnimationEdit
{
	float? StartTime { get; }
	float? EndTime { get; }

	IReadOnlySet<AnimationTrack> GetAffectedTracks( AnimationClip clip );

	T Apply<T>( AnimationTrack track, T value, float time );
}
