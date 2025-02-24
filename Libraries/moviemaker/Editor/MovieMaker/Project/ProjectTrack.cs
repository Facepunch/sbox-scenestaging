using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Sandbox.MovieMaker;
using Sandbox.MovieMaker.Compiled;

namespace Editor.MovieMaker;

#nullable enable

public abstract partial class ProjectTrack( MovieProject project, Guid id, string name, Type targetType ) : ITrack
{
	public MovieProject Project => project;
	public Guid Id => id;
	public string Name => name;
	public Type TargetType => targetType;
	public ProjectTrack? Parent => throw new NotImplementedException();
	public bool IsEmpty => throw new NotImplementedException();

	public IReadOnlyList<ProjectTrack> Children => throw new NotImplementedException();

	public void Remove() => throw new NotImplementedException();

	public void RemoveBlocks() => throw new NotImplementedException();
	public ProjectBlock AddBlock( IBlock block ) => throw new NotImplementedException();
	public ProjectBlock GetBlock( MovieTime time ) => throw new NotImplementedException();

	ITrack? ITrack.Parent => Parent;

	public abstract Track Compile( Track? compiledParent );
}

public sealed class ProjectReferenceTrack<T>( MovieProject project, Guid id, string name )
	: ProjectTrack( project, id, name, typeof(T) ), IReferenceTrack<T>
{
	public override Track Compile( Track? compiledParent ) =>
		new ReferenceTrack<T>( Id, Name, (ReferenceTrack<GameObject>)compiledParent! );
}

public abstract class ProjectPropertyTrack( MovieProject project, Guid id, string name, Type targetType )
	: ProjectTrack( project, id, name, targetType ), IPropertyTrack, IBlockTrack<ProjectBlock>
{
	public MovieTimeRange TimeRange => throw new NotImplementedException();
	public IReadOnlyList<ProjectBlock> Blocks => throw new NotImplementedException();
	public abstract IReadOnlyList<(MovieTimeRange TimeRange, ProjectBlock Block)> Cuts { get; }
	public abstract IReadOnlyList<(MovieTimeRange TimeRange, ProjectBlock Block)> GetCuts( MovieTimeRange timeRange );

	public abstract bool TryGetValue( MovieTime time, out object? value );

	IReadOnlyList<IBlock> IBlockTrack.Blocks => throw new NotImplementedException();

	ITrack IPropertyTrack.Parent => Parent!;
}

public sealed class ProjectPropertyTrack<T>( MovieProject project, Guid id, string name )
	: ProjectPropertyTrack( project, id, name, typeof(T) ), IPropertyTrack<T>
{
	public override IReadOnlyList<(MovieTimeRange TimeRange, ProjectBlock Block)> Cuts => throw new NotImplementedException();
	public override IReadOnlyList<(MovieTimeRange TimeRange, ProjectBlock Block)> GetCuts( MovieTimeRange timeRange ) => throw new NotImplementedException();

	public override Track Compile( Track? compiledParent )
	{
		var compiled = new PropertyTrack<T>( Name, compiledParent! );

		if ( Keyframes is { } keyframes )
		{
			var blocks = keyframes.Compile( Project.SampleRate );

			compiled = compiled with { Blocks = [..blocks.Cast<PropertyBlock<T>>()] };
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
