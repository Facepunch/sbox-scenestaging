using System.Linq;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

public sealed partial class MovieProjectTrack( MovieProject project, Guid id, string name, Type propertyType ) : IMovieTrackDescription
{
	public MovieProject Project => project;
	public Guid Id => id;
	public string Name => name;
	public Type PropertyType => propertyType;
	public MovieProjectTrack? Parent => throw new NotImplementedException();
	public bool IsEmpty => Blocks.Count == 0;

	public IReadOnlyList<MovieProjectTrack> Children => throw new NotImplementedException();
	public IReadOnlyList<MovieProjectBlock> Blocks => throw new NotImplementedException();

	public void Remove() => throw new NotImplementedException();

	public void RemoveBlocks() => throw new NotImplementedException();
	public MovieProjectBlock AddBlock( MovieTimeRange timeRange, IBlockData data ) => throw new NotImplementedException();
	public MovieProjectBlock AddBlock( IMovieBlock block ) => AddBlock( block.TimeRange, block.Data );
	public MovieProjectBlock GetBlock( MovieTime time ) => throw new NotImplementedException();

	public IReadOnlyList<(MovieTimeRange TimeRange, MovieProjectBlock Block)> Cuts => throw new NotImplementedException();
	public IReadOnlyList<(MovieTimeRange TimeRange, MovieProjectBlock Block)> GetCuts( MovieTimeRange timeRange ) => throw new NotImplementedException();

	public MovieTrack Compile() => new ( Id, Name, PropertyType, [..Children.Select( x => x.Compile() )], [..Blocks.Select( x => x.Compile() )] );
}
