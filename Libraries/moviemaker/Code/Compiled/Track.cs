using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Sandbox.MovieMaker.Compiled;

#nullable enable

/// <inheritdoc cref="ITrack"/>
public abstract record CompiledTrack( string Name, Type TargetType, CompiledTrack? Parent ) : ITrack
{
	ITrack? ITrack.Parent => Parent;

	/// <summary>
	/// Create a root <see cref="CompiledReferenceTrack"/> that targets a <see cref="Sandbox.GameObject"/> with
	/// the given <paramref name="name"/>. To create a nested track, use <see cref="CompiledClipExtensions.GameObject"/>.
	/// </summary>
	public static CompiledReferenceTrack<GameObject> GameObject( string name ) => new( Guid.NewGuid(), name );

	/// <summary>
	/// Create a root <see cref="CompiledReferenceTrack"/> that targets a <see cref="Sandbox.Component"/> with
	/// the given <paramref name="type"/>. To create a nested track, use <see cref="CompiledClipExtensions.Component"/>.
	/// </summary>
	public static CompiledReferenceTrack Component( Type type ) =>
		TypeLibrary.GetType( typeof(CompiledReferenceTrack<>) )
			.CreateGeneric<CompiledReferenceTrack>( [type], [Guid.NewGuid(), type.Name, null] );

	/// <summary>
	/// Create a root <see cref="CompiledReferenceTrack"/> that targets a <see cref="Sandbox.Component"/> with
	/// the type <typeparamref name="T"/>. To create a nested track, use <see cref="CompiledClipExtensions.Component{T}"/>.
	/// </summary>
	public static CompiledReferenceTrack<T> Component<T>()
		where T : Component => new( Guid.NewGuid(), typeof(T).Name );
}

/// <inheritdoc cref="IReferenceTrack"/>
public abstract record CompiledReferenceTrack(
	Guid Id,
	string Name,
	Type TargetType,
	CompiledReferenceTrack<GameObject>? Parent )
	: CompiledTrack( Name, TargetType, Parent ), IReferenceTrack
{
	public new CompiledReferenceTrack<GameObject>? Parent => (CompiledReferenceTrack<GameObject>?)base.Parent;

	IReferenceTrack<GameObject>? IReferenceTrack.Parent => Parent;
}

public sealed record CompiledReferenceTrack<T>(
	Guid Id,
	string Name,
	CompiledReferenceTrack<GameObject>? Parent = null )
	: CompiledReferenceTrack( Id, Name, typeof(T), Parent ), IReferenceTrack<T>
	where T : class, IValid;

internal interface IBlockTrack
{
	protected static MovieTimeRange GetTimeRange( IReadOnlyList<CompiledBlock> blocks ) =>
		blocks.Count > 0 ? (blocks.Min( x => x.TimeRange.Start ), blocks.Max( x => x.TimeRange.End )) : default;

	MovieTimeRange TimeRange { get; }
	IReadOnlyList<CompiledBlock> Blocks { get; }
}

/// <inheritdoc cref="IActionTrack"/>
public sealed record CompiledActionTrack(
	string Name,
	Type TargetType,
	CompiledTrack Parent,
	ImmutableArray<CompiledActionBlock> Blocks )
	: CompiledTrack( Name, TargetType, Parent ), IActionTrack, IBlockTrack
{
	public new CompiledTrack Parent => base.Parent!;

	IReadOnlyList<CompiledBlock> IBlockTrack.Blocks => Blocks;

	public MovieTimeRange TimeRange { get; } = IBlockTrack.GetTimeRange( Blocks );

	ITrack IActionTrack.Parent => Parent!;
}

/// <inheritdoc cref="IPropertyTrack"/>
public abstract record CompiledPropertyTrack( string Name, Type TargetType, CompiledTrack Parent )
	: CompiledTrack( Name, TargetType, Parent ), IPropertyTrack, IBlockTrack
{
	public new CompiledTrack Parent => base.Parent!;
	public abstract MovieTimeRange TimeRange { get; }
	public IReadOnlyList<CompiledPropertyBlock> Blocks => OnGetBlocks();

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

	public CompiledPropertyBlock? GetBlock( MovieTime time ) => OnGetBlock( time );

	protected abstract CompiledPropertyBlock? OnGetBlock( MovieTime time );
	protected abstract IReadOnlyList<CompiledPropertyBlock> OnGetBlocks();

	ITrack IPropertyTrack.Parent => Parent;
	IReadOnlyList<CompiledBlock> IBlockTrack.Blocks => OnGetBlocks();
}

/// <inheritdoc cref="IPropertyTrack{T}"/>
public sealed record CompiledPropertyTrack<T>(
	string Name,
	CompiledTrack Parent,
	ImmutableArray<CompiledPropertyBlock<T>> Blocks )
	: CompiledPropertyTrack( Name, typeof(T), Parent ), IPropertyTrack<T>, IBlockTrack
{
	private readonly bool _validated = Validate( Blocks );

	public override MovieTimeRange TimeRange { get; } = IBlockTrack.GetTimeRange( Blocks );
	public new ImmutableArray<CompiledPropertyBlock<T>> Blocks { get; init; } = Blocks;

	IReadOnlyList<CompiledBlock> IBlockTrack.Blocks => Blocks;

	public new CompiledPropertyBlock<T>? GetBlock( MovieTime time )
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

	protected override CompiledPropertyBlock? OnGetBlock( MovieTime time ) => GetBlock( time );
	protected override IReadOnlyList<CompiledPropertyBlock> OnGetBlocks() => Blocks;

	private static bool Validate( ImmutableArray<CompiledPropertyBlock<T>> blocks )
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
