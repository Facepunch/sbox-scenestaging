using System;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Sandbox.MovieMaker;

#nullable enable

/// <summary>
/// A compiled timeline of <see cref="MovieTrack"/>s describing properties changing over time and actions being invoked.
/// </summary>
/// <param name="Tracks">All tracks within the clip.</param>
public sealed partial record MovieClip( params ImmutableArray<MovieTrack> Tracks ) : ValidatedRecord
{
	public static MovieClip Empty { get; } = new();

	private readonly ImmutableDictionary<Guid, MovieTrack> _trackDict = Tracks
		.DistinctBy( x => x.Id )
		.ToImmutableDictionary( x => x.Id, x => x );

	private readonly ImmutableDictionary<Guid, ImmutableArray<MovieTrack>> _childDict = Tracks
		.Where( x => x.Parent is not null )
		.GroupBy( x => x.Parent!.Id )
		.ToImmutableDictionary( x => x.Key, x => x.ToImmutableArray() );

	/// <summary>
	/// Tracks in the clip that don't have parents.
	/// </summary>
	[JsonIgnore]
	public ImmutableArray<MovieTrack> RootTracks { get; } = [..Tracks.Where( x => x.Parent is null )];

	/// <summary>
	/// How long this clip takes to fully play.
	/// </summary>
	[JsonIgnore]
	public MovieTime Duration { get; } = Tracks
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
	/// Gets sub-tracks immediately nested within the track with given <paramref name="trackId"/>.
	/// </summary>
	public ImmutableArray<MovieTrack> GetChildren( Guid trackId )
	{
		return _childDict.GetValueOrDefault( trackId, ImmutableArray<MovieTrack>.Empty );
	}

	/// <summary>
	/// Gets sub-tracks immediately nested within the given <paramref name="track"/>.
	/// </summary>
	public ImmutableArray<MovieTrack> GetChildren( ITrackDescription track ) => GetChildren( track.Id );

	protected override void OnValidate()
	{
		var trackDict = new Dictionary<Guid, MovieTrack>();

		// IDs must be unique

		foreach ( var track in Tracks )
		{
			if ( !trackDict.TryAdd( track.Id, track ) )
			{
				throw new ArgumentException( "Tracks must have unique IDs.", nameof(Tracks) );
			}
		}

		// Parents must be in Tracks too

		foreach ( var track in Tracks )
		{
			if ( track.Parent is null ) continue;

			if ( trackDict!.GetValueOrDefault( track.Parent.Id ) != track.Parent )
			{
				throw new ArgumentException( "All parent tracks must be included in track array.", nameof(Tracks) );
			}
		}

		// No cycles!

		var visited = new HashSet<MovieTrack>();

		foreach ( var track in Tracks )
		{
			visited.Clear();

			var parent = track;

			while ( parent is not null )
			{
				if ( !visited.Add( parent ) )
				{
					throw new ArgumentException( "Track hierarchy must not have cycles.", nameof( Tracks ) );
				}

				parent = parent.Parent;
			}
		}
	}
}
