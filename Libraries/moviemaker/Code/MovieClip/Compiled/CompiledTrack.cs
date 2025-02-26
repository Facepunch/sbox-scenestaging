using System;
using System.Collections.Immutable;

namespace Sandbox.MovieMaker.Compiled;

#nullable enable

/// <summary>
/// Describes how a <see cref="ITrackTarget"/> is animated by a <see cref="CompiledClip"/>.
/// Tracks contain non-overlapping <see cref="CompiledBlock"/>s, which are spans of time for which values or actions are defined.
/// </summary>
/// <param name="Id">ID for referencing this track. Must be unique in this <see cref="CompiledClip"/>.</param>
/// <param name="Name">Property or object name, used when auto-resolving this track in a scene.</param>
/// <param name="TargetType">What type of property is this track controlling.</param>
/// <param name="Parent">Optional track that contains this one is nested within. Used to auto-bind this</param>
/// <param name="Blocks">Time ranges where this track has values defined. Must not overlap, and be in time order.</param>
public sealed record CompiledTrack( Guid Id, string Name, Type TargetType, CompiledTrack? Parent = null, params ImmutableArray<CompiledBlock> Blocks )
	: ValidatedRecord, ITrack
{
	/// <summary>
	/// Time range for which this track has blocks.
	/// </summary>
	public MovieTimeRange TimeRange { get; } = Blocks.Length > 0
		? (Blocks[0].TimeRange.Start, Blocks[^1].TimeRange.End)
		: default;

	public CompiledBlock? GetBlock( MovieTime time )
	{
		if ( !TimeRange.Contains( time ) ) return null;

		// TODO: binary search?

		// We go backwards because if we're exactly on a block boundary, we want to use the later block

		for ( var i = Blocks.Length - 1; i >= 0; --i )
		{
			var block = Blocks[i];

			if ( block.TimeRange.Start > time ) continue;
			if ( block.TimeRange.End < time ) break;

			return block;
		}

		return null;
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

			if ( block is IValueBlock { ValueType: var type } && type != TargetType )
			{
				throw new ArgumentException( $"Value blocks must match {nameof(TargetType)}.", nameof(Blocks) );
			}
		}
	}

	ITrack? ITrack.Parent => Parent;

	private ReadOnlyListWrapper<CompiledBlock, IBlock>? _blocksWrapper;

	IReadOnlyList<IBlock> ITrack.Blocks => _blocksWrapper ??= new( Blocks );
	IBlock? ITrack.GetBlock( MovieTime time ) => GetBlock( time );
}
