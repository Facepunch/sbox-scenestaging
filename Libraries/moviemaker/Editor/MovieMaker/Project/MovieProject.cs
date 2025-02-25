using System.Linq;
using System.Text.Json.Nodes;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

/// <summary>
/// All the info needed to compile a <see cref="MovieClip"/>. Gets serialized
/// and stored in <see cref="IMovieSource.EditorData"/>.
/// </summary>
public sealed class MovieProject : IJsonPopulator
{
	public int SampleRate { get; set; } = 30;

	public bool IsEmpty => throw new NotImplementedException();
	public MovieTime Duration => throw new NotImplementedException();

	public IReadOnlyList<MovieProjectTrack> Tracks => throw new NotImplementedException();
	public IReadOnlyList<MovieProjectTrack> RootTracks => throw new NotImplementedException();

	public MovieProjectTrack? GetTrack( Guid trackId )
	{
		throw new NotImplementedException();
	}

	public MovieClip Compile() => new ( [..RootTracks.Select( x => x.Compile() )] );

	public JsonNode Serialize()
	{
		throw new NotImplementedException();
	}

	public void Deserialize( JsonNode node )
	{
		throw new NotImplementedException();
	}

	public MovieProjectTrack AddTrack( string name, Type propertyType, MovieProjectTrack? parentTrack = null )
	{
		throw new NotImplementedException();
	}
}
