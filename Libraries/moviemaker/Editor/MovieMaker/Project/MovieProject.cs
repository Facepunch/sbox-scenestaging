using System.Collections.Immutable;
using System.Linq;
using System.Text.Json.Nodes;
using Sandbox.MovieMaker;
using Sandbox.MovieMaker.Compiled;

namespace Editor.MovieMaker;

#nullable enable

/// <summary>
/// All the info needed to compile a <see cref="CompiledMovieClip"/>. Gets serialized
/// and stored in <see cref="IMovieSource.EditorData"/>.
/// </summary>
public sealed class MovieProject : IJsonPopulator
{
	public int SampleRate { get; set; } = 30;

	public bool IsEmpty => throw new NotImplementedException();
	public MovieTime Duration => throw new NotImplementedException();

	public IReadOnlyList<ProjectTrack> Tracks => throw new NotImplementedException();
	public IReadOnlyList<ProjectTrack> RootTracks => throw new NotImplementedException();

	public ProjectTrack? GetTrack( Guid trackId )
	{
		throw new NotImplementedException();
	}

	public Clip Compile()
	{
		var result = new Dictionary<ProjectTrack, Track>();

		foreach ( var track in RootTracks )
		{
			CompileTrack( track, result );
		}

		return new Clip( result.Values );
	}

	private void CompileTrack( ProjectTrack track, Dictionary<ProjectTrack, Track> result ) =>
		result.Add( track, track.Compile( track.Parent is { } parent ? result[parent] : null ) );

	public JsonNode Serialize()
	{
		throw new NotImplementedException();
	}

	public void Deserialize( JsonNode node )
	{
		throw new NotImplementedException();
	}

	public ProjectTrack AddTrack( string name, Type propertyType, ProjectTrack? parentTrack = null )
	{
		throw new NotImplementedException();
	}
}
