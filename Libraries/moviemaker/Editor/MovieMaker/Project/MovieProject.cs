using System.Collections.Immutable;
using System.Linq;
using System.Text.Json.Nodes;
using Sandbox.MovieMaker;

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

	public IReadOnlyList<MovieProjectTrack> Tracks => throw new NotImplementedException();
	public IReadOnlyList<MovieProjectTrack> RootTracks => throw new NotImplementedException();

	public MovieProjectTrack? GetTrack( Guid trackId )
	{
		throw new NotImplementedException();
	}

	public CompiledMovieClip Compile()
	{
		var result = new List<CompiledTrack>();

		foreach ( var track in RootTracks )
		{
			CompileTrack( track, result );
		}

		return new CompiledMovieClip( [..result] );
	}

	private void CompileTrack( MovieProjectTrack track, List<CompiledTrack> result ) =>
		result.Add( track.Compile( track.Parent is { } parent ? result.First( x => x.Id == parent.Id ) : null ) );

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
