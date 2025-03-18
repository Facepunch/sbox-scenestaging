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
public sealed partial class MovieProject
{
	private readonly Dictionary<Guid, ProjectSourceClip> _sourceClipDict = new();

	private readonly List<IProjectTrack> _rootTrackList = new();
	private readonly List<IProjectTrack> _trackList = new();
	private readonly Dictionary<Guid, IProjectTrack> _trackDict = new();

	private bool _tracksChanged;

	/// <summary>
	/// When compiling, what sample rate to use.
	/// </summary>
	public int SampleRate { get; set; } = 30;

	public bool IsEmpty => Tracks.Count == 0 && SourceClips.Count == 0;

	public MovieTime Duration => _trackList.OfType<IProjectPropertyTrack>()
		.Select( x => x.TimeRange.End )
		.DefaultIfEmpty( 0d )
		.Max();

	public IReadOnlyDictionary<Guid, ProjectSourceClip> SourceClips => _sourceClipDict;

	public IReadOnlyList<IProjectTrack> Tracks
	{
		get
		{
			UpdateTracks();
			return _trackList;
		}
	}

	public IReadOnlyList<IProjectTrack> RootTracks
	{
		get
		{
			UpdateTracks();
			return _rootTrackList;
		}
	}

	public IProjectTrack? GetTrack( Guid trackId )
	{
		UpdateTracks();
		return _trackDict!.GetValueOrDefault( trackId );
	}

	public CompiledClip Compile()
	{
		var result = new Dictionary<IProjectTrack, CompiledTrack>();

		foreach ( var track in RootTracks )
		{
			CompileTrack( track, result );
		}

		return new CompiledClip( result.Values );
	}

	private void CompileTrack( IProjectTrack track, Dictionary<IProjectTrack, CompiledTrack> result ) =>
		result.Add( track, track.Compile( track.Parent is { } parent ? result[parent] : null, false ) );

	public ProjectSourceClip AddSourceClip( CompiledClip clip, JsonObject? metadata = null )
	{
		var guid = Guid.NewGuid();

		return _sourceClipDict[guid] = new ProjectSourceClip( guid, clip, metadata );
	}

	public IProjectReferenceTrack AddReferenceTrack( string name, Type targetType, IProjectTrack? parentTrack = null )
	{
		var guid = Guid.NewGuid();
		var track = IProjectReferenceTrack.Create( this, guid, name, targetType );

		AddTrackInternal( (IProjectTrackInternal)track, (IProjectTrackInternal?)parentTrack );

		return track;
	}

	public IProjectPropertyTrack AddPropertyTrack( string name, Type targetType, IProjectTrack? parentTrack = null )
	{
		var guid = Guid.NewGuid();
		var track = IProjectPropertyTrack.Create( this, guid, name, targetType );

		AddTrackInternal( (IProjectTrackInternal)track, (IProjectTrackInternal?)parentTrack );

		return track;
	}

	private void AddTrackInternal( IProjectTrackInternal track, IProjectTrackInternal? parentTrack )
	{
		if ( _trackDict.ContainsKey( track.Id ) )
		{
			throw new Exception( "Conflicting track IDs!" );
		}

		parentTrack?.AddChild( track );

		_trackList.Add( track );
		_tracksChanged = true;
	}

	internal void RemoveTrackInternal( IProjectTrackInternal projectTrack )
	{
		if ( !_trackList.Remove( projectTrack ) ) return;

		foreach ( var child in projectTrack.Children.ToArray() )
		{
			child.Remove();
		}

		projectTrack.Parent?.RemoveChild( projectTrack );

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
