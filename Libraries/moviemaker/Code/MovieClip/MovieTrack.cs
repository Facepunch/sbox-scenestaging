using System;
using System.Collections.Immutable;

namespace Sandbox.MovieMaker;

#nullable enable

/// <summary>
/// Describes how a <see cref="IMovieProperty"/> is animated by a <see cref="MovieClip"/>.
/// Tracks contain non-overlapping <see cref="MovieBlock"/>s, which are spans of time for which values or actions are defined.
/// </summary>
/// <param name="Id">ID for referencing this track. Must be unique in this <see cref="MovieClip"/>.</param>
/// <param name="Name">Property or object name, used when auto-resolving this track in a scene.</param>
/// <param name="PropertyType">What type of property is this track controlling.</param>
/// <param name="Children">Tracks representing nested properties inside this track.</param>
/// <param name="Blocks">Blocks contained in this track, ordered by ascending start time, with no overlaps.</param>
public sealed partial record MovieTrack( Guid Id, string Name, Type PropertyType, ImmutableArray<MovieTrack> Children, ImmutableArray<MovieBlock> Blocks )
	: ValidatedRecord, IMovieTrackDescription
{
	/// <summary>
	/// Time range for which this track has blocks.
	/// </summary>
	public MovieTimeRange TimeRange { get; } = Blocks.Length > 0
		? (Blocks[0].TimeRange.Start, Blocks[^1].TimeRange.End)
		: default;

	public MovieTrack( string Name, Type PropertyType, params ImmutableArray<MovieBlock> Blocks )
		: this( Guid.NewGuid(), Name, PropertyType, ImmutableArray<MovieTrack>.Empty, Blocks )
	{

	}

	protected override void OnValidate()
	{
		if ( Blocks.Length == 0 ) return;

		var prevTime = Blocks[0].TimeRange.Start;

		if ( prevTime < MovieTime.Zero )
		{
			throw new ArgumentException( "Blocks must have non-negative start times.", nameof(Blocks) );
		}

		foreach ( var block in Blocks )
		{
			if ( block.TimeRange.Start < prevTime )
			{
				throw new ArgumentException( "Blocks must not overlap.", nameof(Blocks) );
			}

			if ( block.Data is IValueData { ValueType: var type } && type != PropertyType )
			{
				throw new ArgumentException( $"Block data must match {nameof(PropertyType)}.", nameof(Blocks) );
			}
		}
	}
}
