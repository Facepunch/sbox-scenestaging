using System;
using System.Collections.Immutable;

namespace Sandbox.MovieMaker;

#nullable enable

/// <summary>
/// Describes how a <see cref="ITrackTarget"/> is animated by a <see cref="MovieClip"/>.
/// Tracks contain non-overlapping <see cref="MovieBlock"/>s, which are spans of time for which values or actions are defined.
/// </summary>
/// <param name="Id">ID for referencing this track. Must be unique in this <see cref="MovieClip"/>.</param>
/// <param name="Name">Property or object name, used when auto-resolving this track in a scene.</param>
/// <param name="TargetType">What type of property is this track controlling.</param>
/// <param name="Parent">Optional track that contains this one is nested within. Used to auto-bind this</param>
/// <param name="Blocks">Tracks can be nested, which means child tracks can auto-bind to targets in the scene if their parent is bound.</param>
public sealed record MovieTrack( Guid Id, string Name, Type TargetType, MovieTrack? Parent = null, params ImmutableArray<MovieBlock> Blocks )
	: ValidatedRecord, ITrackDescription
{
	/// <summary>
	/// Time range for which this track has blocks.
	/// </summary>
	public MovieTimeRange TimeRange { get; } = Blocks.Length > 0
		? (Blocks[0].TimeRange.Start, Blocks[^1].TimeRange.End)
		: default;

	/// <summary>
	/// How deeply are we nested? Root tracks have depth <c>0</c>.
	/// </summary>
	internal int Depth => Parent is null ? 0 : Parent.Depth + 1;

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

			if ( block.Data is IValueData { ValueType: var type } && type != TargetType )
			{
				throw new ArgumentException( $"Block data must match {nameof(TargetType)}.", nameof(Blocks) );
			}
		}
	}

	ITrackDescription? ITrackDescription.Parent => Parent;
}
