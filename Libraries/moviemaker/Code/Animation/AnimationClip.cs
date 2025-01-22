using System;

namespace Sandbox.Animation;

#nullable enable

/// <summary>
/// Contains a list of tracks that describe changes over time.
/// </summary>
public sealed class AnimationClip
{
	private readonly List<AnimationTrack> _tracks = new();

	/// <summary>
	/// List of animation tracks in this clip.
	/// </summary>
	public IReadOnlyList<AnimationTrack> Tracks => _tracks;

	/// <summary>
	/// Adds a new animation track to this clip, with the given type.
	/// </summary>
	/// <param name="type">Property type for this track.</param>
	public AnimationTrack AddTrack( Type type )
	{
		var nextId = _tracks.Count == 0 ? 1 : _tracks[^1].Id + 1;
		var track = new AnimationTrack( this, nextId, type );

		_tracks.Add( track );

		return track;
	}

	/// <summary>
	/// Should only be called from <see cref="AnimationTrack.Remove"/>
	/// </summary>
	internal void RemoveTrackInternal( AnimationTrack animationTrack )
	{
		_tracks.Remove( animationTrack );
	}
}
