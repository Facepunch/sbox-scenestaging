using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Channels;
using Sandbox.MovieMaker;
using Sandbox.MovieMaker.Compiled;
using static Editor.Button;

namespace Editor.MovieMaker;

#nullable enable

public interface IProjectTrack : ITrack, IComparable<IProjectTrack>
{
	Guid Id { get; }

	MovieProject Project { get; }
	new IProjectTrack? Parent { get; }
	IReadOnlyList<IProjectTrack> Children { get; }

	bool IsEmpty { get; }

	void Remove();
	IProjectTrack? GetChild( string name );

	CompiledTrack Compile( CompiledTrack? compiledParent, bool headerOnly );

	ITrack? ITrack.Parent => Parent;

	int IComparable<IProjectTrack>.CompareTo( IProjectTrack? other )
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
}

internal interface IProjectTrackInternal : IProjectTrack
{
	new IProjectTrackInternal? Parent { get; set; }

	void AddChild( IProjectTrackInternal child );
	void RemoveChild( IProjectTrackInternal child );
}

public abstract partial class ProjectTrack<T>( MovieProject project, Guid id, string name ) : IProjectTrackInternal
{
	private readonly List<IProjectTrack> _children = new();
	private bool _childrenChanged;

	public MovieProject Project { get; } = project;
	public Guid Id { get; } = id;
	public string Name { get; } = name;
	public Type TargetType { get; } = typeof(T);

	public IProjectTrack? Parent { get; private set; }

	public virtual bool IsEmpty => Children.Count == 0;

	public IReadOnlyList<IProjectTrack> Children
	{
		get
		{
			UpdateChildren();
			return _children;
		}
	}

	public void Remove() => Project.RemoveTrackInternal( this );

	public IProjectTrack? GetChild( string name ) => Children.FirstOrDefault( x => x.Name == name );

	public abstract CompiledTrack Compile( CompiledTrack? compiledParent, bool headerOnly );

	IProjectTrackInternal? IProjectTrackInternal.Parent
	{
		get => (IProjectTrackInternal?)Parent;
		set => Parent = value;
	}

	void IProjectTrackInternal.AddChild( IProjectTrackInternal child )
	{
		if ( child.Parent != null )
		{
			throw new ArgumentException( "Track already has a parent!", nameof(child) );
		}

		child.Parent = this;
		_children.Add( child );
		_childrenChanged = true;
	}

	void IProjectTrackInternal.RemoveChild( IProjectTrackInternal child )
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

public interface IProjectReferenceTrack : IProjectTrack, IReferenceTrack
{
	public static IProjectReferenceTrack Create( MovieProject project, Guid id, string name, Type targetType )
	{
		var trackType = typeof(ProjectReferenceTrack<>).MakeGenericType( targetType );

		return (IProjectReferenceTrack)Activator.CreateInstance( trackType, project, id, name )!;
	}

	new ProjectReferenceTrack<GameObject>? Parent { get; }
	new Guid Id { get; }

	IReferenceTrack<GameObject>? IReferenceTrack.Parent => Parent;
	IProjectTrack? IProjectTrack.Parent => Parent;

	Guid IReferenceTrack.Id => Id;
	Guid IProjectTrack.Id => Id;
}

public sealed class ProjectReferenceTrack<T>( MovieProject project, Guid id, string name )
	: ProjectTrack<T>( project, id, name ), IProjectReferenceTrack, IReferenceTrack<T>
	where T : class, IValid
{
	public new ProjectReferenceTrack<GameObject>? Parent => (ProjectReferenceTrack<GameObject>?)base.Parent;

	public override CompiledTrack Compile( CompiledTrack? compiledParent, bool headerOnly ) =>
		new CompiledReferenceTrack<T>( Id, Name, (CompiledReferenceTrack<GameObject>)compiledParent! );

	ITrack? ITrack.Parent => Parent;
}

public interface IProjectPropertyTrack : IPropertyTrack, IProjectTrack
{
	public static IProjectPropertyTrack Create( MovieProject project, Guid id, string name, Type targetType )
	{
		var trackType = typeof(ProjectPropertyTrack<>).MakeGenericType( targetType );

		return (IProjectPropertyTrack)Activator.CreateInstance( trackType, project, id, name )!;
	}

	new IProjectTrack? Parent { get; }

	IReadOnlyList<IProjectPropertyBlock> Blocks { get; }
	MovieTimeRange TimeRange { get; }

	ITrack IPropertyTrack.Parent => Parent!;

	/// <summary>
	/// Add empty space from the start of <paramref name="timeRange"/>, with
	/// the duration of <paramref name="timeRange"/>. Will split any blocks that
	/// span the start time.
	/// </summary>
	bool Insert( MovieTimeRange timeRange );

	/// <summary>
	/// Remove the given <paramref name="timeRange"/>, then collapse the removed
	/// time range so any blocks after the end of the range will start earlier.
	/// </summary>
	bool Remove( MovieTimeRange timeRange );

	/// <summary>
	/// Remove any blocks within the <paramref name="timeRange"/>, splitting any
	/// blocks that span the start or end. This doesn't shift any blocks, so will
	/// leave an empty region of time.
	/// </summary>
	bool Clear( MovieTimeRange timeRange );

	/// <summary>
	/// Adds a <paramref name="block"/>, replacing any blocks that overlap its time range.
	/// This will split any blocks that partially overlap.
	/// </summary>
	bool Add( IProjectPropertyBlock block );

	/// <summary>
	/// Copies blocks that overlap the given <paramref name="timeRange"/> and returns
	/// the copies.
	/// </summary>
	IReadOnlyList<IProjectPropertyBlock> Slice( MovieTimeRange timeRange );

	IReadOnlyList<IProjectPropertyBlock> CreateSourceBlocks( ProjectSourceClip source );

	IProjectTrack? IProjectTrack.Parent => Parent;
	ITrack? ITrack.Parent => Parent;
}

public sealed class ProjectPropertyTrack<T>( MovieProject project, Guid id, string name )
	: ProjectTrack<T>( project, id, name ), IProjectPropertyTrack, IPropertyTrack<T>
{
	private readonly List<PropertyBlock<T>> _blocks = new();
	private bool _blocksChanged;

	public MovieTimeRange TimeRange => (0d, Blocks.Select( x => x.TimeRange.End )
		.DefaultIfEmpty()
		.Max());

	public IReadOnlyList<PropertyBlock<T>> Blocks
	{
		get
		{
			UpdateBlocks();
			return _blocks;
		}
	}

	IReadOnlyList<IProjectPropertyBlock> IProjectPropertyTrack.Blocks => Blocks;

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

	public bool Insert( MovieTimeRange timeRange )
	{
		return Clear( timeRange.Start )
			| Shift( timeRange.Start, timeRange.Duration );
	}

	public bool Remove( MovieTimeRange timeRange )
	{
		return Clear( timeRange )
			| Shift( timeRange.End, -timeRange.Duration );
	}

	private bool Shift( MovieTime from, MovieTime offset )
	{
		var changed = false;

		for ( var i = 0; i < _blocks.Count; ++i )
		{
			var block = _blocks[i];

			if ( block.TimeRange.Start >= from )
			{
				_blocks[i] = _blocks[i].Shift( offset );
				_blocksChanged = changed = true;
			}
		}

		return changed;
	}

	public bool Clear( MovieTimeRange timeRange )
	{
		var overlaps = this.GetBlocks( timeRange ).ToArray();

		if ( overlaps.Length == 0 ) return false;

		foreach ( var overlap in overlaps )
		{
			_blocks.Remove( overlap );

			if ( overlap.TimeRange.Start < timeRange.Start )
			{
				_blocks.Add( overlap.Slice( (overlap.TimeRange.Start, timeRange.Start) )! );
			}

			if ( overlap.TimeRange.End > timeRange.End )
			{
				_blocks.Add( overlap.Slice( (timeRange.End, overlap.TimeRange.End) )! );
			}
		}

		_blocksChanged = true;

		return true;
	}

	public bool Add( PropertyBlock<T> block )
	{
		// Track blocks can't go before the start of the track!

		if ( block.Slice( block.TimeRange.ClampStart( 0d ) ) is not { } slice ) return false;

		Clear( slice.TimeRange );

		_blocksChanged = true;
		_blocks.Add( block );

		return true;
	}

	bool IProjectPropertyTrack.Add( IProjectPropertyBlock block ) => Add( (PropertyBlock<T>)block );

	public IReadOnlyList<PropertyBlock<T>> Slice( MovieTimeRange timeRange )
	{
		return Blocks
			.Select( x => x.Slice( timeRange ) )
			.OfType<PropertyBlock<T>>()
			.ToImmutableArray();
	}

	IReadOnlyList<IProjectPropertyBlock> IProjectPropertyTrack.Slice( MovieTimeRange timeRange ) => Slice( timeRange );

	public IReadOnlyList<PropertyBlock<T>> CreateSourceBlocks( ProjectSourceClip source )
	{
		throw new NotImplementedException();
	}

	IReadOnlyList<IProjectPropertyBlock> IProjectPropertyTrack.CreateSourceBlocks( ProjectSourceClip source ) =>
		CreateSourceBlocks( source );

	private void UpdateBlocks()
	{
		if ( !_blocksChanged ) return;

		_blocksChanged = false;

		_blocks.Sort( ( a, b ) => a.TimeRange.Start.CompareTo( b.TimeRange.Start ) );
	}
}
