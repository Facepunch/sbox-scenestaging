using System;

namespace Sandbox.Animation;

#nullable enable

/// <summary>
/// A track from an <see cref="AnimationClip"/> that describes a value that can change over time.
/// </summary>
public class AnimationTrack : IValid
{
	private AnimationClip? _clip;

	/// <summary>
	/// Which clip contains this track.
	/// </summary>
	public AnimationClip Clip => _clip ?? throw new Exception( $"{nameof(AnimationTrack)} '{DisplayName}' has been removed." );

	/// <summary>
	/// ID number for referencing this track, unique in the containing clip.
	/// </summary>
	public int Id { get; }

	/// <summary>
	/// User-facing name of this track.
	/// </summary>
	public string DisplayName { get; set; }

	/// <summary>
	/// What type of property is this track.
	/// </summary>
	public Type Type { get; }

	/// <summary>
	/// False if this track has been removed.
	/// </summary>
	public bool IsValid => _clip is not null;

	internal AnimationTrack( AnimationClip clip, int id, Type type )
	{
		_clip = clip;

		Id = id;
		Type = type;

		// Placeholder name

		DisplayName = $"Track {id}";
	}

	/// <summary>
	/// Remove this track from the clip.
	/// </summary>
	public void Remove()
	{
		_clip?.RemoveTrackInternal( this );
		_clip = null;
	}
}
