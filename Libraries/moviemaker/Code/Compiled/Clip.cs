using System;
using System.Collections.Immutable;

namespace Sandbox.MovieMaker.Compiled;

#nullable enable

/// <summary>
/// An immutable compiled <see cref="IClip"/> designed to be serialized.
/// </summary>
public sealed partial class CompiledClip : IClip
{
	/// <summary>
	/// A clip with no tracks.
	/// </summary>
	public static CompiledClip Empty { get; } = new();

	private readonly ImmutableDictionary<Guid, CompiledReferenceTrack> _referenceTracks;

	/// <inheritdoc cref="IClip.Tracks"/>
	public ImmutableArray<CompiledTrack> Tracks { get; }

	public MovieTime Duration { get; }

	public CompiledClip( params CompiledTrack[] tracks )
		: this( tracks.AsEnumerable() )
	{

	}

	public CompiledClip( IEnumerable<CompiledTrack> tracks )
	{
		var allTracks = new HashSet<CompiledTrack>();

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

		var referenceTracks = new Dictionary<Guid, CompiledReferenceTrack>();

		// IDs must be unique

		foreach ( var track in allTracks.OfType<CompiledReferenceTrack>() )
		{
			if ( !referenceTracks.TryAdd( track.Id, track ) )
			{
				throw new ArgumentException( "Tracks must have unique IDs.", nameof( Tracks ) );
			}
		}

		// Initialize

		// ReSharper disable once UseCollectionExpression
		Tracks = allTracks.OrderBy( x => x.GetDepth() ).ThenBy( x => x.Name ).ToImmutableArray();

		_referenceTracks = referenceTracks.ToImmutableDictionary();

		Duration = allTracks
			.OfType<IBlockTrack>()
			.Select( x => x.TimeRange.End )
			.DefaultIfEmpty().Max();
	}

	/// <inheritdoc cref="IClip.GetTrack"/>
	public CompiledReferenceTrack? GetTrack( Guid trackId )
	{
		return _referenceTracks.GetValueOrDefault( trackId );
	}

	IEnumerable<ITrack> IClip.Tracks => Tracks.CastArray<ITrack>();
	IReferenceTrack? IClip.GetTrack( Guid trackId ) => GetTrack( trackId );
}
