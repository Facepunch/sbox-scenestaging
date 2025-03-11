using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Sandbox.MovieMaker;
using Sandbox.MovieMaker.Compiled;

namespace Editor.MovieMaker;

#nullable enable

public abstract partial class ProjectTrack( MovieProject project, string name, Type targetType ) : ITrack
{
	public MovieProject Project { get; } = project;
	public string Name { get; } = name;
	public Type TargetType { get; } = targetType;

	public ProjectTrack? Parent => throw new NotImplementedException();
	public bool IsEmpty => throw new NotImplementedException();

	public IReadOnlyList<ProjectTrack> Children => throw new NotImplementedException();

	public void Remove() => throw new NotImplementedException();

	public ProjectTrack? GetChild( string name ) => Children.FirstOrDefault( x => x.Name == name );

	ITrack? ITrack.Parent => Parent;

	public abstract CompiledTrack Compile( CompiledTrack? compiledParent, bool headerOnly );
}

public abstract class ProjectReferenceTrack( MovieProject project, Guid id, string name, Type targetType )
	: ProjectTrack( project, name, targetType ), IReferenceTrack
{
	public Guid Id => id;
	public new ProjectReferenceTrack<GameObject>? Parent => (ProjectReferenceTrack<GameObject>?)base.Parent;

	IReferenceTrack<GameObject>? IReferenceTrack.Parent => Parent;
}

public sealed class ProjectReferenceTrack<T>( MovieProject project, Guid id, string name )
	: ProjectReferenceTrack( project, id, name, typeof(T) ), IReferenceTrack<T>
{
	public override CompiledTrack Compile( CompiledTrack? compiledParent, bool headerOnly ) =>
		new CompiledReferenceTrack<T>( Id, Name, (CompiledReferenceTrack<GameObject>)compiledParent! );
}

public abstract class ProjectPropertyTrack( MovieProject project, string name, Type targetType )
	: ProjectTrack( project, name, targetType ), IPropertyTrack
{
	public KeyframeCurve? Keyframes { get; set; }

	public IReadOnlyList<PropertyBlock> Blocks => OnGetBlocks();
	protected abstract IReadOnlyList<PropertyBlock> OnGetBlocks();

	public MovieTimeRange TimeRange => Blocks is { Count: > 0 } blocks
		? (blocks[0].TimeRange.Start, blocks[^1].TimeRange.End)
		: default;

	public abstract IReadOnlyList<MovieTime> Cuts { get; }

	public abstract bool TryGetValue( MovieTime time, out object? value );

	ITrack IPropertyTrack.Parent => Parent!;

	/// <summary>
	/// Add empty space from the start of <paramref name="timeRange"/>, with
	/// the duration of <paramref name="timeRange"/>. Will split any blocks that
	/// span the start time.
	/// </summary>
	public abstract bool Insert( MovieTimeRange timeRange );

	/// <summary>
	/// Remove the given <paramref name="timeRange"/>, then collapse the removed
	/// time range so any blocks after the end of the range will start earlier.
	/// </summary>
	public abstract bool Remove( MovieTimeRange timeRange );

	/// <summary>
	/// Remove any blocks within the <paramref name="timeRange"/>, splitting any
	/// blocks that span the start or end. This doesn't shift any blocks, so will
	/// leave an empty region of time.
	/// </summary>
	public abstract bool Clear( MovieTimeRange timeRange );

	/// <summary>
	/// Adds a <paramref name="block"/>, replacing any blocks that overlap its time range.
	/// This will split any blocks that partially overlap.
	/// </summary>
	public abstract bool Add( PropertyBlock block );

	/// <summary>
	/// Copies blocks that overlap the given <paramref name="timeRange"/> and returns
	/// the copies.
	/// </summary>
	public IEnumerable<PropertyBlock> Slice( MovieTimeRange timeRange )
	{
		throw new NotImplementedException();
	}
}

public sealed class ProjectPropertyTrack<T>( MovieProject project, string name )
	: ProjectPropertyTrack( project, name, typeof(T) ), IPropertyTrack<T>
{
	private readonly List<PropertyBlock<T>> _blocks = new();

	public new IReadOnlyList<PropertyBlock<T>> Blocks => _blocks;

	protected override IReadOnlyList<PropertyBlock> OnGetBlocks() => _blocks;

	public override IReadOnlyList<MovieTime> Cuts => throw new NotImplementedException();

	public override CompiledTrack Compile( CompiledTrack? compiledParent, bool headerOnly )
	{
		var compiled = new CompiledPropertyTrack<T>( Name, compiledParent!, [] );

		if ( headerOnly ) return compiled;

		return compiled with { Blocks = [..Blocks.Select( x => x.Compile( this ) )] };
	}

	public bool TryGetValue( MovieTime time, [MaybeNullWhen( false )] out T value )
	{
		throw new NotImplementedException();
	}

	public override bool TryGetValue( MovieTime time, out object? value )
	{
		if ( TryGetValue( time, out var val ) )
		{
			value = val;
			return true;
		}

		value = null;
		return false;
	}

	public override bool Insert( MovieTimeRange timeRange )
	{
		throw new NotImplementedException();
	}

	public override bool Remove( MovieTimeRange timeRange )
	{
		throw new NotImplementedException();
	}

	public override bool Clear( MovieTimeRange timeRange )
	{
		throw new NotImplementedException();
	}

	public override bool Add( PropertyBlock block )
	{
		throw new NotImplementedException();
	}

	public new IEnumerable<PropertyBlock<T>> Slice( MovieTimeRange timeRange ) =>
		base.Slice( timeRange ).Cast<PropertyBlock<T>>();
}

public record struct TrackPath( Guid ReferenceId, ImmutableArray<string> PropertyNames )
{
	public static TrackPath FromTrack( ITrack track )
	{
		if ( track is IReferenceTrack refTrack )
		{
			return new TrackPath( refTrack.Id, ImmutableArray<string>.Empty );
		}

		if ( track.Parent is not { } parent )
		{
			throw new ArgumentException( "Expected root track to be a reference track.", nameof(track) );
		}

		var parentPath = FromTrack( parent );

		return parentPath with { PropertyNames = [..parentPath.PropertyNames, track.Name] };
	}
}
