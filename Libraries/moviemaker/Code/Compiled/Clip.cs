using System;
using System.Collections.Immutable;

namespace Sandbox.MovieMaker.Compiled;

#nullable enable

/// <summary>
/// An immutable compiled <see cref="IClip"/> designed to be serialized.
/// </summary>
public sealed partial class Clip : IClip
{
	/// <summary>
	/// A clip with no tracks.
	/// </summary>
	public static Clip Empty { get; } = new();

	private readonly ImmutableDictionary<Guid, ReferenceTrack> _referenceTracks;

	/// <inheritdoc cref="IClip.Tracks"/>
	public ImmutableArray<Track> Tracks { get; }

	public MovieTime Duration { get; }

	public Clip( params Track[] tracks )
		: this( tracks.AsEnumerable() )
	{

	}

	public Clip( IEnumerable<Track> tracks )
	{
		var allTracks = new HashSet<Track>();

		// Find all root tracks

		foreach ( var track in tracks )
		{
			var parent = track;

			while ( parent is not null && allTracks.Add( parent ) )
			{
				parent = parent.Parent;

				// No cycles!

				if ( parent == track )
				{
					throw new ArgumentException( "Track hierarchy must not have cycles.", nameof( Tracks ) );
				}
			}
		}

		var referenceTracks = new Dictionary<Guid, ReferenceTrack>();

		// IDs must be unique

		foreach ( var track in allTracks.OfType<ReferenceTrack>() )
		{
			if ( !referenceTracks.TryAdd( track.Id, track ) )
			{
				throw new ArgumentException( "Tracks must have unique IDs.", nameof( Tracks ) );
			}
		}

		// Initialize

		Tracks = [.. allTracks.OrderBy( x => x.GetDepth() ).ThenBy( x => x.Name )];

		_referenceTracks = referenceTracks.ToImmutableDictionary();

		Duration = allTracks
			.OfType<IBlockTrack>()
			.Select( x => x.TimeRange.End )
			.DefaultIfEmpty().Max();
	}

	/// <inheritdoc cref="IClip.GetTrack"/>
	public ReferenceTrack? GetTrack( Guid trackId )
	{
		return _referenceTracks.GetValueOrDefault( trackId );
	}

	IEnumerable<ITrack> IClip.Tracks => Tracks.CastArray<ITrack>();
	IReferenceTrack? IClip.GetTrack( Guid trackId ) => GetTrack( trackId );
}
