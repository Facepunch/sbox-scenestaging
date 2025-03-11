using System.Collections.Immutable;
using System.Linq;
using System.Text.Json.Nodes;
using Sandbox.MovieMaker;
using Sandbox.MovieMaker.Compiled;

namespace Editor.MovieMaker;

#nullable enable

/// <summary>
/// All the info needed to compile a <see cref="CompiledClip"/>. Gets serialized
/// and stored in <see cref="IMovieResource.EditorData"/>.
/// </summary>
public sealed class MovieProject : IJsonPopulator
{
	public int SampleRate { get; set; } = 30;

	public bool IsEmpty => throw new NotImplementedException();
	public MovieTime Duration => throw new NotImplementedException();

	public IReadOnlyList<ProjectTrack> Tracks => throw new NotImplementedException();
	public IReadOnlyList<ProjectTrack> RootTracks => throw new NotImplementedException();

	public ProjectReferenceTrack? GetTrack( Guid trackId ) => Tracks
		.OfType<ProjectReferenceTrack>()
		.FirstOrDefault( x => x.Id == trackId );

	public ProjectTrack? GetTrack( TrackPath path )
	{
		ProjectTrack? track = GetTrack( path.ReferenceId );

		return path.PropertyNames.Aggregate( track,
			( current, propertyName ) => current?.GetChild( propertyName ) );
	}

	public CompiledClip Compile()
	{
		var result = new Dictionary<ProjectTrack, CompiledTrack>();

		foreach ( var track in RootTracks )
		{
			CompileTrack( track, result );
		}

		return new CompiledClip( result.Values );
	}

	private void CompileTrack( ProjectTrack track, Dictionary<ProjectTrack, CompiledTrack> result ) =>
		result.Add( track, track.Compile( track.Parent is { } parent ? result[parent] : null ) );

	public JsonNode Serialize()
	{
		throw new NotImplementedException();
	}

	public void Deserialize( JsonNode node )
	{
		throw new NotImplementedException();
	}

	public ProjectReferenceTrack AddReferenceTrack( string name, Type targetType, ProjectTrack? parentTrack = null )
	{
		throw new NotImplementedException();
	}

	public ProjectPropertyTrack AddPropertyTrack( string name, Type targetType, ProjectTrack? parentTrack = null )
	{
		throw new NotImplementedException();
	}
}
