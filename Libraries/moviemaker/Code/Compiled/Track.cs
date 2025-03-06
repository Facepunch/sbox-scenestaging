using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Sandbox.MovieMaker.Compiled;

#nullable enable

/// <inheritdoc cref="ITrack"/>
public abstract record Track( string Name, Type TargetType, Track? Parent ) : ITrack
{
	ITrack? ITrack.Parent => Parent;

	/// <summary>
	/// Create a root <see cref="ReferenceTrack"/> that targets a <see cref="Sandbox.GameObject"/> with
	/// the given <paramref name="name"/>. To create a nested track, use <see cref="CompiledClipExtensions.GameObject"/>.
	/// </summary>
	public static ReferenceTrack<GameObject> GameObject( string name ) => new( Guid.NewGuid(), name );

	/// <summary>
	/// Create a root <see cref="ReferenceTrack"/> that targets a <see cref="Sandbox.Component"/> with
	/// the given <paramref name="type"/>. To create a nested track, use <see cref="CompiledClipExtensions.Component"/>.
	/// </summary>
	public static ReferenceTrack Component( Type type ) =>
		TypeLibrary.GetType( typeof( ReferenceTrack<> ) )
			.CreateGeneric<ReferenceTrack>( [type], [Guid.NewGuid(), type.Name, null] );

	/// <summary>
	/// Create a root <see cref="ReferenceTrack"/> that targets a <see cref="Sandbox.Component"/> with
	/// the type <typeparamref name="T"/>. To create a nested track, use <see cref="CompiledClipExtensions.Component{T}"/>.
	/// </summary>
	public static ReferenceTrack<T> Component<T>() => new ( Guid.NewGuid(), typeof(T).Name );
}

/// <inheritdoc cref="IReferenceTrack"/>
public abstract record ReferenceTrack( Guid Id, string Name, Type TargetType, ReferenceTrack<GameObject>? Parent )
	: Track( Name, TargetType, Parent ), IReferenceTrack
{
	public new ReferenceTrack<GameObject>? Parent => (ReferenceTrack<GameObject>?)base.Parent;

	IReferenceTrack<GameObject>? IReferenceTrack.Parent => Parent;
}

public sealed record ReferenceTrack<T>( Guid Id, string Name, ReferenceTrack<GameObject>? Parent = null )
	: ReferenceTrack( Id, Name, typeof(T), Parent ), IReferenceTrack<T>;

internal interface IBlockTrack
{
	protected static MovieTimeRange GetTimeRange( IReadOnlyList<Block> blocks ) =>
		blocks.Count > 0 ? (blocks.Min( x => x.TimeRange.Start ), blocks.Max( x => x.TimeRange.End )) : default;

	MovieTimeRange TimeRange { get; }
	IReadOnlyList<Block> Blocks { get; }
}

/// <inheritdoc cref="IActionTrack"/>
public sealed record ActionTrack( string Name, Type TargetType, Track Parent, ImmutableArray<ActionBlock> Blocks )
	: Track( Name, TargetType, Parent ), IActionTrack, IBlockTrack
{
	public new Track Parent => base.Parent!;

	IReadOnlyList<Block> IBlockTrack.Blocks => Blocks;

	public MovieTimeRange TimeRange { get; } = IBlockTrack.GetTimeRange( Blocks );

	ITrack IActionTrack.Parent => Parent!;
}

/// <inheritdoc cref="IPropertyTrack"/>
public abstract record PropertyTrack( string Name, Type TargetType, Track Parent )
	: Track( Name, TargetType, Parent ), IPropertyTrack
{
	public new Track Parent => base.Parent!;

	public bool TryGetValue( MovieTime time, out object? value )
	{
		if ( OnGetBlock( time ) is { } block )
		{
			value = block.GetValue( time );
			return true;
		}

		value = null;
		return false;
	}

	public PropertyBlock? GetBlock( MovieTime time ) => OnGetBlock( time );

	protected abstract PropertyBlock? OnGetBlock( MovieTime time );

	ITrack IPropertyTrack.Parent => Parent!;
}

/// <inheritdoc cref="IPropertyTrack{T}"/>
public sealed record PropertyTrack<T>( string Name, Track Parent, ImmutableArray<PropertyBlock<T>> Blocks )
	: PropertyTrack( Name, typeof(T), Parent ), IPropertyTrack<T>, IBlockTrack
{
	private readonly bool _validated = Validate( Blocks );

	IReadOnlyList<Block> IBlockTrack.Blocks => Blocks;

	public MovieTimeRange TimeRange { get; } = IBlockTrack.GetTimeRange( Blocks );

	public new PropertyBlock<T>? GetBlock( MovieTime time )
	{
		if ( !TimeRange.Contains( time ) ) return default;

		// TODO: binary search?

		// We go backwards because if we're exactly on a block boundary, we want to use the later block

		for ( var i = Blocks.Length - 1; i >= 0; --i )
		{
			var block = Blocks[i];

			if ( block.TimeRange.Start > time ) continue;
			if ( block.TimeRange.End < time ) break;

			return block;
		}

		return default;
	}

	public bool TryGetValue( MovieTime time, [MaybeNullWhen( false )] out T value )
	{
		if ( GetBlock( time ) is { } block )
		{
			value = block.GetValue( time );
			return true;
		}

		value = default;
		return false;
	}

	protected override PropertyBlock? OnGetBlock( MovieTime time ) => GetBlock( time );

	private static bool Validate( ImmutableArray<PropertyBlock<T>> blocks )
	{
		if ( blocks.IsDefault )
		{
			throw new ArgumentException( "Blocks must be an array.", nameof(Blocks) );
		}

		if ( blocks.Length == 0 ) return true;

		var prevTime = blocks[0].TimeRange.Start;

		if ( prevTime < 0d )
		{
			throw new ArgumentException( "Blocks must have non-negative start times.", nameof(Blocks) );
		}

		foreach ( var block in blocks )
		{
			if ( block.TimeRange.Start < prevTime )
			{
				throw new ArgumentException( "Blocks must not overlap.", nameof(Blocks) );
			}
		}

		return true;
	}
}
