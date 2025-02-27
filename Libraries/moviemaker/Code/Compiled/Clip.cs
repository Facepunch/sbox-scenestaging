using System;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Sandbox.MovieMaker.Compiled;

#nullable enable

/// <summary>
/// An immutable compiled <see cref="IClip"/> designed to be serialized, and efficient to play back.
/// </summary>
/// <param name="Tracks">All tracks within the clip.</param>
public sealed partial record Clip( params ImmutableArray<Track> Tracks ) : ValidatedRecord, IClip
{
	/// <summary>
	/// A clip with no tracks.
	/// </summary>
	public static Clip Empty { get; } = new();

	private readonly ImmutableDictionary<Guid, Track> _trackDict = Tracks
		.DistinctBy( x => x.Id )
		.ToImmutableDictionary( x => x.Id, x => x );

	[JsonIgnore]
	public MovieTime Duration { get; } = Tracks
		.OfType<IBlockTrack>()
		.Select( x => x.TimeRange.End )
		.DefaultIfEmpty().Max();

	/// <inheritdoc cref="IClip.GetTrack"/>
	public Track? GetTrack( Guid trackId )
	{
		return _trackDict.GetValueOrDefault( trackId );
	}

	IReadOnlyList<ITrack> IClip.Tracks => Tracks.CastArray<ITrack>();
	ITrack? IClip.GetTrack( Guid trackId ) => GetTrack( trackId );

	protected override void OnValidate()
	{
		var trackDict = new Dictionary<Guid, Track>();

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

		var visited = new HashSet<Track>();

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
