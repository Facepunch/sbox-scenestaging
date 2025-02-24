using Sandbox.MovieMaker;
using Sandbox.MovieMaker.Compiled;

namespace Editor.MovieMaker;

#nullable enable

public sealed class ProjectBlock : IBlock
{
	public MovieProject Project => Track.Project;
	public ProjectTrack Track => throw new NotImplementedException();

	public MovieTimeRange TimeRange { get; set; }

	public Block Compile() => throw new NotImplementedException();

	public void Remove() => throw new NotImplementedException();
}
