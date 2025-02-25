using System;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Sandbox.MovieMaker;

#nullable enable

/// <summary>
/// A compiled timeline of <see cref="MovieTrack"/>s describing properties changing over time and actions being invoked.
/// </summary>
/// <param name="RootTracks">Set of tracks in this clip that are at the root level in the hierarchy.</param>
public sealed record MovieClip( params ImmutableArray<MovieTrack> RootTracks ) : ValidatedRecord
{
	private readonly ImmutableDictionary<Guid, MovieTrack> _trackDict = RootTracks
		.SelectMany( EnumerateDescendants )
		.DistinctBy( x => x.Id )
		.ToImmutableDictionary( x => x.Id, x => x );

	/// <summary>
	/// All tracks in this clip, including children of other tracks.
	/// </summary>
	[JsonIgnore]
	public ImmutableArray<MovieTrack> Tracks { get; } = [..RootTracks.SelectMany( EnumerateDescendants )];

	/// <summary>
	/// How long this clip takes to fully play.
	/// </summary>
	[JsonIgnore]
	public MovieTime Duration { get; } = RootTracks
		.SelectMany( EnumerateDescendants )
		.Select( x => x.TimeRange.End )
		.DefaultIfEmpty().Max();

	/// <summary>
	/// Attempts to get a track with the given <paramref name="trackId"/>.
	/// </summary>
	/// <returns>The matching track, or <see langword="null"/> if not found.</returns>
	public MovieTrack? GetTrack( Guid trackId )
	{
		return _trackDict.GetValueOrDefault( trackId );
	}

	/// <summary>
	/// Attempts to get a root track with the given <paramref name="name"/>.
	/// </summary>
	/// <returns>The matching track, or <see langword="null"/> if not found.</returns>
	public MovieTrack? this[ string name ] => RootTracks.FirstOrDefault( x => x.Name == name );

	protected override void OnValidate()
	{
		var allUniqueIds = RootTracks
			.SelectMany( EnumerateDescendants )
			.GroupBy( x => x.Id )
			.All( x => x.Count() == 1 );

		if ( !allUniqueIds )
		{
			throw new ArgumentException( "Tracks must have unique IDs.", nameof(Tracks) );
		}
	}

	private static IEnumerable<MovieTrack> EnumerateDescendants( MovieTrack track ) =>
		[track, ..track.Children.SelectMany( EnumerateDescendants )];
}
