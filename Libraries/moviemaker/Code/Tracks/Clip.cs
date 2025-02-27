using System;

namespace Sandbox.MovieMaker;

#nullable enable

/// <summary>
/// A collection of <see cref="ITrack"/>s describing properties changing over time and actions being invoked.
/// </summary>
public interface IClip
{
	/// <summary>
	/// All tracks within the clip.
	/// </summary>
	IReadOnlyList<ITrack> Tracks { get; }

	/// <summary>
	/// How long this clip takes to fully play.
	/// </summary>
	MovieTime Duration { get; }

	/// <summary>
	/// Attempts to get a track with the given <paramref name="trackId"/>.
	/// </summary>
	/// <returns>The matching track, or <see langword="null"/> if not found.</returns>
	public ITrack? GetTrack( Guid trackId );
}
