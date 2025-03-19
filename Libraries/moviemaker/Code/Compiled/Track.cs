using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Sandbox.MovieMaker.Compiled;

#nullable enable

/// <inheritdoc cref="ITrack"/>
public interface ICompiledTrack : ITrack
{
	new ICompiledTrack? Parent { get; }
}

/// <inheritdoc cref="IReferenceTrack"/>
public interface ICompiledReferenceTrack : ICompiledTrack, IReferenceTrack
{
	new CompiledReferenceTrack<GameObject>? Parent { get; }

	ICompiledTrack? ICompiledTrack.Parent => Parent;
	IReferenceTrack<GameObject>? IReferenceTrack.Parent => Parent;
}

public interface ICompiledBlockTrack : ICompiledTrack
{
	IReadOnlyList<ICompiledBlock> Blocks { get; }

	public MovieTimeRange TimeRange => Blocks.Count == 0 ? default : (Blocks[0].TimeRange.Start, Blocks[^1].TimeRange.End);
}

public sealed record CompiledReferenceTrack<T>(
	Guid Id,
	string Name,
	CompiledReferenceTrack<GameObject>? Parent = null )
	: ICompiledReferenceTrack, IReferenceTrack<T> where T : class, IValid;

/// <inheritdoc cref="IActionTrack"/>
public sealed record CompiledActionTrack(
	string Name,
	Type TargetType,
	ICompiledTrack Parent,
	ImmutableArray<CompiledActionBlock> Blocks )
	: IActionTrack, ICompiledBlockTrack
{
	public IEnumerable<CompiledActionBlock> GetBlocks( MovieTime time ) =>
		Blocks.Where( x => x.TimeRange.Contains( time ) );

	ITrack IActionTrack.Parent => Parent;
	IReadOnlyList<ICompiledBlock> ICompiledBlockTrack.Blocks => Blocks;
}

/// <inheritdoc cref="IPropertyTrack"/>
public interface ICompiledPropertyTrack : IPropertyTrack, ICompiledBlockTrack
{
	new ICompiledTrack Parent { get; }
	new IReadOnlyList<ICompiledPropertyBlock> Blocks { get; }

	ICompiledPropertyBlock? GetBlock( MovieTime time );

	ITrack IPropertyTrack.Parent => Parent;
	IReadOnlyList<ICompiledBlock> ICompiledBlockTrack.Blocks => Blocks;
}

/// <inheritdoc cref="IPropertyTrack{T}"/>
public sealed record CompiledPropertyTrack<T>(
	string Name,
	ICompiledTrack Parent,
	ImmutableArray<ICompiledPropertyBlock<T>> Blocks )
	: ICompiledPropertyTrack, IPropertyTrack<T>
{
	private readonly ImmutableArray<ICompiledPropertyBlock<T>> _blocks = Validate( Blocks );

	public ImmutableArray<ICompiledPropertyBlock<T>> Blocks
	{
		get => _blocks;
		init => _blocks = Validate( value );
	}

	public ICompiledPropertyBlock<T>? GetBlock( MovieTime time )
	{
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

	IReadOnlyList<ICompiledPropertyBlock> ICompiledPropertyTrack.Blocks => Blocks;
	ICompiledPropertyBlock? ICompiledPropertyTrack.GetBlock( MovieTime time ) => GetBlock( time );

	private static ImmutableArray<ICompiledPropertyBlock<T>> Validate( ImmutableArray<ICompiledPropertyBlock<T>> blocks )
	{
		if ( blocks.IsDefault )
		{
			throw new ArgumentException( "Blocks must be an array.", nameof(Blocks) );
		}

		if ( blocks.Length == 0 ) return blocks;

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

		return blocks;
	}
}
