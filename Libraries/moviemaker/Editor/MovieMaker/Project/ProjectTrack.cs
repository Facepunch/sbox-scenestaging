using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Sandbox.MovieMaker;
using Sandbox.MovieMaker.Compiled;

namespace Editor.MovieMaker;

#nullable enable

public abstract partial class ProjectTrack( MovieProject project, Guid id, string name, Type targetType ) : ITrack, IComparable<ProjectTrack>
{
	private readonly List<ProjectTrack> _children = new();
	private bool _childrenChanged;

	public MovieProject Project { get; } = project;
	public Guid Id { get; } = id;
	public string Name { get; } = name;
	public Type TargetType { get; } = targetType;

	public ProjectTrack? Parent { get; private set; }
	public virtual bool IsEmpty => Children.Count == 0;

	public IReadOnlyList<ProjectTrack> Children
	{
		get
		{
			UpdateChildren();
			return _children;
		}
	}

	public void Remove() => Project.RemoveTrackInternal( this );

	public ProjectTrack? GetChild( string name ) => Children.FirstOrDefault( x => x.Name == name );

	ITrack? ITrack.Parent => Parent;

	public abstract CompiledTrack Compile( CompiledTrack? compiledParent, bool headerOnly );

	public int CompareTo( ProjectTrack? other )
	{
		if ( ReferenceEquals( this, other ) )
		{
			return 0;
		}

		if ( other is null )
		{
			return 1;
		}

		var depthComparison = this.GetDepth().CompareTo( other.GetDepth() );
		return depthComparison != 0 ? depthComparison : string.Compare( Name, other.Name, StringComparison.Ordinal );
	}

	internal void AddChildInternal( ProjectTrack child )
	{
		if ( child.Parent != null )
		{
			throw new ArgumentException( "Track already has a parent!", nameof(child) );
		}

		child.Parent = this;
		_children.Add( child );
		_childrenChanged = true;
	}

	internal void RemoveChildInternal( ProjectTrack child )
	{
		_children.Remove( child );
		_childrenChanged = true;
	}

	private void UpdateChildren()
	{
		if ( !_childrenChanged ) return;

		_childrenChanged = false;
		_children.Sort();
	}
}

public abstract class ProjectReferenceTrack( MovieProject project, Guid id, string name, Type targetType )
	: ProjectTrack( project, id, name, targetType ), IReferenceTrack
{
	public static ProjectReferenceTrack Create( MovieProject project, Guid id, string name, Type targetType )
	{
		var trackType = typeof(ProjectReferenceTrack<>).MakeGenericType( targetType );

		return (ProjectReferenceTrack)Activator.CreateInstance( trackType, project, id, name )!;
	}

	public new ProjectReferenceTrack<GameObject>? Parent => (ProjectReferenceTrack<GameObject>?)base.Parent;

	IReferenceTrack<GameObject>? IReferenceTrack.Parent => Parent;
}

public sealed class ProjectReferenceTrack<T>( MovieProject project, Guid id, string name )
	: ProjectReferenceTrack( project, id, name, typeof(T) ), IReferenceTrack<T>
	where T : class, IValid
{
	public override CompiledTrack Compile( CompiledTrack? compiledParent, bool headerOnly ) =>
		new CompiledReferenceTrack<T>( Id, Name, (CompiledReferenceTrack<GameObject>)compiledParent! );
}

public abstract class ProjectPropertyTrack( MovieProject project, Guid id, string name, Type targetType )
	: ProjectTrack( project, id, name, targetType ), IPropertyTrack
{
	public static ProjectPropertyTrack Create( MovieProject project, Guid id, string name, Type targetType )
	{
		var trackType = typeof(ProjectPropertyTrack<>).MakeGenericType( targetType );

		return (ProjectPropertyTrack)Activator.CreateInstance( trackType, project, id, name )!;
	}

	public KeyframeCurve? Keyframes { get; set; }

	public IReadOnlyList<PropertyBlock> Blocks => OnGetBlocks();
	protected abstract IReadOnlyList<PropertyBlock> OnGetBlocks();

	public override bool IsEmpty => Blocks.Count == 0;

	public MovieTimeRange TimeRange => Blocks is { Count: > 0 } blocks
		? (blocks[0].TimeRange.Start, blocks[^1].TimeRange.End)
		: default;

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
		return Blocks
			.Select( x =>
				x.TimeRange.Intersect( timeRange ) is { Duration.IsPositive: true } intersection
					? x.Slice( intersection )
					: null )
			.OfType<PropertyBlock>();
	}

	public abstract IReadOnlyList<PropertyBlock> CreateSourceBlocks( ProjectSourceClip source );
}

public sealed class ProjectPropertyTrack<T>( MovieProject project, Guid id, string name )
	: ProjectPropertyTrack( project, id, name, typeof(T) ), IPropertyTrack<T>
{
	private readonly List<PropertyBlock<T>> _blocks = new();
	private bool _blocksChanged;

	public new IReadOnlyList<PropertyBlock<T>> Blocks
	{
		get
		{
			UpdateBlocks();
			return _blocks;
		}
	}

	protected override IReadOnlyList<PropertyBlock> OnGetBlocks() => Blocks;

	public override CompiledTrack Compile( CompiledTrack? compiledParent, bool headerOnly )
	{
		var compiled = new CompiledPropertyTrack<T>( Name, compiledParent!, [] );

		if ( headerOnly ) return compiled;

		return compiled with { Blocks = [..Blocks.Select( x => x.Compile( this ) )] };
	}

	public bool TryGetValue( MovieTime time, [MaybeNullWhen( false )] out T value )
	{
		if ( Blocks.GetBlock( time ) is { } block )
		{
			value = block.GetValue( time );
			return true;
		}

		value = default;
		return false;
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
		if ( block is not PropertyBlock<T> typedBlock ) return false;

		if ( typedBlock.TimeRange.End <= 0 ) return false;
		if ( typedBlock.TimeRange.Start < 0 )
		{
			typedBlock = typedBlock.Slice( (0d, typedBlock.TimeRange.End) );
		}

		_blocksChanged = true;

		for ( var i = _blocks.Count - 1; i >= 0; i-- )
		{
			var overlappingBlock = _blocks[i];

			if ( overlappingBlock.TimeRange.Intersect( typedBlock.TimeRange ) is null )
			{
				continue;
			}

			_blocks.RemoveAt( i );

			// Is old block completely inside new block? Remove it.

			if ( typedBlock.TimeRange.Contains( overlappingBlock.TimeRange ) )
			{
				continue;
			}

			// Does old block start before new block? Add the old block head.

			if ( typedBlock.TimeRange.Start > overlappingBlock.TimeRange.Start )
			{
				_blocks.Add( overlappingBlock.Slice( (overlappingBlock.TimeRange.Start, typedBlock.TimeRange.Start) ) );
			}

			// Does old block end after the new block? Add the old block tail.

			if ( typedBlock.TimeRange.End < overlappingBlock.TimeRange.End )
			{
				_blocks.Add( overlappingBlock.Slice( (typedBlock.TimeRange.End, overlappingBlock.TimeRange.End) ) );
			}
		}

		_blocks.Add( typedBlock );

		return true;
	}

	public new IReadOnlyList<PropertyBlock<T>> Slice( MovieTimeRange timeRange ) =>
		base.Slice( timeRange ).Cast<PropertyBlock<T>>().ToArray();

	public override IReadOnlyList<PropertyBlock> CreateSourceBlocks( ProjectSourceClip source )
	{
		throw new NotImplementedException();
	}

	private void UpdateBlocks()
	{
		if ( !_blocksChanged ) return;

		_blocksChanged = false;
		_blocks.Sort( ( a, b ) => a.TimeRange.Start.CompareTo( b.TimeRange.Start ) );
	}
}
