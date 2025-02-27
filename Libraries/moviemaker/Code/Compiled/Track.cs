using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Sandbox.MovieMaker.Compiled;

#nullable enable

/// <summary>
/// Describes how a <see cref="ITarget"/> is animated by a <see cref="Clip"/>.
/// Tracks contain non-overlapping <see cref="Block"/>s, which are spans of time for which values or actions are defined.
/// </summary>
/// <param name="Id">ID for referencing this track. Must be unique in this <see cref="IClip"/>.</param>
/// <param name="Name">Property or object name, used when auto-resolving this track in a scene.</param>
/// <param name="TargetType">What type of property is this track controlling.</param>
/// <param name="Parent">Optional track that contains this one is nested within. Used to auto-bind this</param>
public abstract record Track( Guid Id,
	string Name,
	Type TargetType,
	Track? Parent = null )
	: ValidatedRecord, ITrack
{
	ITrack? ITrack.Parent => Parent;

	public static ReferenceTrack GameObject( string name, Track? parent = null ) => new ReferenceTrack( Guid.NewGuid(), name, typeof(GameObject), parent );
	public static ReferenceTrack Component( Type type, Track? parent = null ) => new ReferenceTrack( Guid.NewGuid(), type.Name, typeof(Component), parent );
	public static ReferenceTrack Component<T>( Track? parent = null ) => Component( typeof(T), parent );
	public static PropertyTrack<T> Property<T>( string name, Track parent ) => new( Guid.NewGuid(), name, parent );
}

public record ReferenceTrack(
	Guid Id,
	string Name,
	Type TargetType,
	Track? Parent = null ) : Track( Id, Name, TargetType, Parent ), IReferenceTrack;

/// <summary>
/// Unused.
/// </summary>
public record ActionTrack(
	Guid Id,
	string Name,
	Type TargetType,
	Track? Parent = null ) : Track( Id, Name, TargetType, Parent ), IActionTrack
{
	public MovieTimeRange TimeRange => default;

	IReadOnlyList<IActionBlock> IBlockTrack<IActionBlock>.Blocks => [];
	IReadOnlyList<IBlock> IBlockTrack.Blocks => [];
}

/// <summary>
/// Describes how a member property in the scene is animated over time when mapped to a <see cref="IProperty{T}"/>.
/// Contain non-overlapping <see cref="PropertyBlock{T}"/>s, which are spans of time for which values are defined.
/// </summary>
/// <typeparam name="T">Target property value type.</typeparam>
/// <param name="Id">ID for referencing this track. Must be unique in this <see cref="IClip"/>.</param>
/// <param name="Name">Property or object name, used when auto-resolving this track in a scene.</param>
/// <param name="Parent">Optional track that contains this one is nested within. Used to auto-bind this</param>
/// <param name="Blocks">Non-overlapping spans of time for which values are defined, in order of ascending start time.</param>
public sealed record PropertyTrack<T>( Guid Id,
	string Name,
	Track Parent,
	params ImmutableArray<PropertyBlock<T>> Blocks )
	: Track( Id, Name, typeof(T), Parent ), IPropertyTrack<T>, IBlockTrack<PropertyBlock<T>>
{
	private ReadOnlyListWrapper<PropertyBlock<T>, IBlock>? _blocksWrapper;

	/// <summary>
	/// Time range for which this track has blocks.
	/// </summary>
	public MovieTimeRange TimeRange { get; } = Blocks.Length > 0
		? (Blocks[0].TimeRange.Start, Blocks[^1].TimeRange.End)
		: default;

	public bool TryGetValue( MovieTime time, [MaybeNullWhen( false )] out T value )
	{
		if ( this.GetBlock( time ) is { } block )
		{
			value = block.GetValue( time );
			return true;
		}

		value = default;
		return false;
	}

	protected override void OnValidate()
	{
		if ( Blocks.Length == 0 ) return;

		var prevTime = Blocks[0].TimeRange.Start;

		if ( prevTime < MovieTime.Zero )
		{
			throw new ArgumentException( "Blocks must have non-negative start times.", nameof( Blocks ) );
		}

		foreach ( var block in Blocks )
		{
			if ( block.TimeRange.Start < prevTime )
			{
				throw new ArgumentException( "Blocks must not overlap.", nameof( Blocks ) );
			}
		}
	}

	IReadOnlyList<IBlock> IBlockTrack.Blocks => _blocksWrapper ??= new( Blocks );
	IReadOnlyList<PropertyBlock<T>> IBlockTrack<PropertyBlock<T>>.Blocks => Blocks;

	bool IPropertyTrack.TryGetValue( MovieTime time, out object? value )
	{
		if ( TryGetValue( time, out var val ) )
		{
			value = val;
			return true;
		}

		value = null;
		return false;
	}
}
