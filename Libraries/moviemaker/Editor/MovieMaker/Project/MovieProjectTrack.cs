using Sandbox.MovieMaker;
using System.Text.Json.Nodes;

namespace Editor.MovieMaker;

#nullable enable

public sealed class MovieProjectTrack( MovieProject project, Guid id, string name, Type propertyType ) : IMovieTrackDescription
{
	public MovieProject Project => project;
	public Guid Id => id;
	public string Name => name;
	public Type PropertyType => propertyType;
	public bool CanRecord => throw new NotImplementedException();

	public JsonNode? Keyframes { get; set; }


}
