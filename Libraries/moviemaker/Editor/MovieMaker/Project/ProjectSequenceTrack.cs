
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json.Serialization;
using Sandbox.MovieMaker;
using Sandbox.MovieMaker.Compiled;

namespace Editor.MovieMaker;

#nullable enable

public sealed partial class ProjectSequenceTrack( MovieProject project, Guid id, string name )
	: ProjectReferenceTrack<GameObject>( project, id, name )
{
	private readonly List<ProjectSequenceBlock> _blocks = new();
	private readonly HashSet<IProjectSequencePropertyTrack> _tracks = new();
	private bool _tracksInvalid = true;

	public override int Order => -2000;

	public override bool IsEmpty => _blocks.Count == 0;

	public IReadOnlyList<ProjectSequenceBlock> Blocks => _blocks;

	public IEnumerable<IProjectSequencePropertyTrack> PropertyTracks
	{
		get
		{
			UpdateTracks();
			return _tracks;
		}
	}

	public ProjectSequenceBlock AddBlock( MovieTimeRange timeRange, MovieTransform transform, MovieResource resource )
	{
		var block = new ProjectSequenceBlock( timeRange, transform, resource );

		_blocks.Add( block );
		_tracksInvalid = true;

		return block;
	}

	public void RemoveBlock( ProjectSequenceBlock block )
	{
		_blocks.Remove( block );
		_tracksInvalid = true;
	}

	public override ICompiledTrack Compile( ICompiledTrack? compiledParent, bool headerOnly )
	{
		return new CompiledReferenceTrack<GameObject>( Id, Name, (CompiledReferenceTrack<GameObject>)compiledParent! );
	}

	private void UpdateTracks()
	{
		if ( !_tracksInvalid ) return;

		_tracksInvalid = false;
		_tracks.Clear();

		var resourceGroups = Blocks
			.GroupBy( x => x.Resource )
			.Where( x => x.Key?.Compiled is not null );

		var propertyTrackGenericType = typeof(ProjectSequencePropertyTrack<>);

		foreach ( var group in resourceGroups )
		{
			foreach ( var track in group.Key!.Compiled!.Tracks )
			{
				if ( track is not IPropertyTrack propertyTrack ) continue;

				var propertyTrackType = propertyTrackGenericType
					.MakeGenericType( track.TargetType );

				var sequencePropertyTrack = (IProjectSequencePropertyTrack)Activator.CreateInstance( propertyTrackType, propertyTrack, group.AsEnumerable() )!;

				_tracks.Add( sequencePropertyTrack );
			}
		}
	}
}

public sealed class ProjectSequenceBlock
	: ITrackBlock
{
	public MovieTimeRange TimeRange { get; set; }
	public MovieTransform Transform { get; set; }
	public MovieResource Resource { get; }

	[JsonConstructor]
	public ProjectSequenceBlock( MovieTimeRange timeRange, MovieTransform transform, MovieResource resource )
	{
		TimeRange = timeRange;
		Transform = transform;
		Resource = resource;
	}


}

public interface IProjectSequencePropertyTrack : IPropertyTrack
{
	ICompiledPropertyTrack Compile();
}

/// <summary>
/// A property track from a referenced <see cref="MovieResource"/>, with block transformations applied from a <see cref="ProjectSequenceBlock"/>.
/// </summary>
file sealed class ProjectSequencePropertyTrack<T> : IProjectSequencePropertyTrack, IPropertyTrack<T>
{
	public CompiledPropertyTrack<T> SourceTrack { get; }
	public ImmutableArray<ProjectSequenceBlock> Blocks { get; }

	public string Name => SourceTrack.Name;

	public ITrack Parent => SourceTrack.Parent;

	public ICompiledPropertyTrack Compile()
	{
		throw new NotImplementedException();
	}

	public ProjectSequencePropertyTrack( CompiledPropertyTrack<T> sourceTrack, IEnumerable<ProjectSequenceBlock> blocks )
	{
		SourceTrack = sourceTrack;
		Blocks = [..blocks];
	}

	public bool TryGetValue( MovieTime time, [MaybeNullWhen( false )] out T value )
	{
		if ( Blocks.GetBlock( time ) is not { } sequenceBlock )
		{
			value = default;
			return false;
		}

		return SourceTrack.TryGetValue( sequenceBlock.Transform.Inverse * time, out value );
	}
}
