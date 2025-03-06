using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Sandbox.MovieMaker;
using Sandbox.MovieMaker.Compiled;

namespace Editor.MovieMaker;

#nullable enable

public abstract partial class ProjectTrack( MovieProject project, string name, Type targetType ) : ITrack
{
	public MovieProject Project => project;
	public string Name => name;
	public Type TargetType => targetType;
	public ProjectTrack? Parent => throw new NotImplementedException();
	public bool IsEmpty => throw new NotImplementedException();

	public IReadOnlyList<ProjectTrack> Children => throw new NotImplementedException();

	public void Remove() => throw new NotImplementedException();

	ITrack? ITrack.Parent => Parent;

	public abstract CompiledTrack Compile( CompiledTrack? compiledParent );
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
	public override CompiledTrack Compile( CompiledTrack? compiledParent ) =>
		new ReferenceTrack<T>( Id, Name, (ReferenceTrack<GameObject>)compiledParent! );
}

public abstract class ProjectPropertyTrack( MovieProject project, string name, Type targetType )
	: ProjectTrack( project, name, targetType ), IPropertyTrack
{
	public abstract IReadOnlyList<PropertyBlock> Blocks { get; }

	public MovieTimeRange TimeRange => Blocks is { Count: > 0 } blocks
		? (blocks[0].TimeRange.Start, blocks[^1].TimeRange.End)
		: default;

	public abstract IReadOnlyList<MovieTime> Cuts { get; }

	public abstract bool TryGetValue( MovieTime time, out object? value );

	ITrack IPropertyTrack.Parent => Parent!;

	public abstract void RemoveBlocks();
	public void AddBlock( PropertyBlock block ) => OnAddBlock( block );
	protected abstract void OnAddBlock( PropertyBlock block );

	public PropertyBlock? GetBlock( MovieTime time ) => OnGetBlock( time );
	protected abstract PropertyBlock? OnGetBlock( MovieTime time );
}

public sealed class ProjectPropertyTrack<T>( MovieProject project, string name )
	: ProjectPropertyTrack( project, name, typeof(T) ), IPropertyTrack<T>
{
	public override IReadOnlyList<MovieTime> Cuts => throw new NotImplementedException();

	public override CompiledTrack Compile( CompiledTrack? compiledParent )
	{
		var compiled = new PropertyTrack<T>( Name, compiledParent!, [] );

		if ( Keyframes is { } keyframes )
		{
			var blocks = keyframes.Compile( Project.SampleRate );

			compiled = compiled with { Blocks = [..blocks.Cast<CompiledPropertyBlock<T>>()] };
		}

		return compiled;
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
}
