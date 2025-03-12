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
	private readonly Dictionary<Guid, ProjectSourceClip> _sourceClipDict = new();

	private readonly List<ProjectTrack> _rootTrackList = new();
	private readonly List<ProjectTrack> _trackList = new();
	private readonly Dictionary<Guid, ProjectTrack> _trackDict = new();

	private bool _tracksChanged;

	/// <summary>
	/// When compiling, what sample rate to use.
	/// </summary>
	public int SampleRate { get; set; } = 30;

	public bool IsEmpty => Tracks.Count == 0 && SourceClips.Count == 0;

	public MovieTime Duration => _trackList.OfType<ProjectPropertyTrack>()
		.Select( x => x.TimeRange.End )
		.DefaultIfEmpty( 0d )
		.Max();

	public IReadOnlyDictionary<Guid, ProjectSourceClip> SourceClips => _sourceClipDict;

	public IReadOnlyList<ProjectTrack> Tracks
	{
		get
		{
			UpdateTracks();
			return _trackList;
		}
	}

	public IReadOnlyList<ProjectTrack> RootTracks
	{
		get
		{
			UpdateTracks();
			return _rootTrackList;
		}
	}

	public ProjectTrack? GetTrack( Guid trackId )
	{
		UpdateTracks();
		return _trackDict!.GetValueOrDefault( trackId );
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
		result.Add( track, track.Compile( track.Parent is { } parent ? result[parent] : null, false ) );

	public JsonNode Serialize()
	{
		throw new NotImplementedException();
	}

	public void Deserialize( JsonNode node )
	{
		throw new NotImplementedException();
	}

	public ProjectSourceClip AddSourceClip( CompiledClip clip, JsonObject? metadata = null )
	{
		var guid = Guid.NewGuid();

		return _sourceClipDict[guid] = new ProjectSourceClip( guid, clip, metadata );
	}

	public ProjectReferenceTrack AddReferenceTrack( string name, Type targetType, ProjectTrack? parentTrack = null )
	{
		var guid = Guid.NewGuid();
		var track = ProjectReferenceTrack.Create( this, guid, name, targetType );

		AddTrackInternal( track, parentTrack );

		return track;
	}

	public ProjectPropertyTrack AddPropertyTrack( string name, Type targetType, ProjectTrack? parentTrack = null )
	{
		var guid = Guid.NewGuid();
		var track = ProjectPropertyTrack.Create( this, guid, name, targetType );

		AddTrackInternal( track, parentTrack );

		return track;
	}

	private void AddTrackInternal( ProjectTrack track, ProjectTrack? parentTrack )
	{
		if ( _trackDict.ContainsKey( track.Id ) )
		{
			throw new Exception( "Conflicting track IDs!" );
		}

		parentTrack?.AddChildInternal( track );

		_trackList.Add( track );
		_tracksChanged = true;
	}

	public void RemoveTrackInternal( ProjectTrack projectTrack )
	{
		if ( !_trackList.Remove( projectTrack ) ) return;

		foreach ( var child in projectTrack.Children.ToArray() )
		{
			child.Remove();
		}

		projectTrack.Parent?.RemoveChildInternal( projectTrack );

		_tracksChanged = true;
	}

	private void UpdateTracks()
	{
		if ( !_tracksChanged ) return;

		_tracksChanged = false;

		_trackList.Sort();

		_rootTrackList.Clear();
		_trackDict.Clear();

		foreach ( var track in _trackList )
		{
			_trackDict[track.Id] = track;

			if ( track.Parent is null )
			{
				_rootTrackList.Add( track );
			}
		}
	}
}
