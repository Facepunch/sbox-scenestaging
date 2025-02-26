using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

public sealed class MovieProjectBlock : IBlock
{
	public MovieProject Project => Track.Project;
	public MovieProjectTrack Track => throw new NotImplementedException();

	public MovieTimeRange TimeRange { get; set; }
	public IBlockData Data { get; set; }

	public MovieBlock Compile() => new ( TimeRange, Data );

	public void Remove() => throw new NotImplementedException();
}
