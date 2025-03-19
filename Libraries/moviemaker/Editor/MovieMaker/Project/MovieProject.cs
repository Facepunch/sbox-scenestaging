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
		var result = new Dictionary<IProjectTrack, ICompiledTrack>();

		foreach ( var track in RootTracks )
		{
			if ( track.IsEmpty ) continue;

			CompileTrack( track, result );
		}

		return CompiledClip.FromTracks( result.Values );
	}

	private void CompileTrack( IProjectTrack track, Dictionary<IProjectTrack, ICompiledTrack> result )
	{
		var compiled = track.Compile( track.Parent is { } parent ? result[parent] : null, false );

		result.Add( track, compiled );

		foreach ( var childTrack in track.Children )
		{
			if ( childTrack.IsEmpty ) continue;

			CompileTrack( childTrack, result );
		}
	}

	public ProjectSourceClip AddSourceClip( CompiledClip clip, JsonObject? metadata = null )
	{
		var guid = Guid.NewGuid();

		return _sourceClipDict[guid] = new ProjectSourceClip( guid, clip, metadata );
	}

	public IProjectReferenceTrack AddReferenceTrack( string name, Type targetType, IProjectTrack? parentTrack = null )
	{
		var guid = Guid.NewGuid();
		var track = IProjectReferenceTrack.Create( this, guid, name, targetType );

		AddTrackInternal( track, parentTrack );

		return track;
	}

	public IProjectPropertyTrack AddPropertyTrack( string name, Type targetType, IProjectTrack? parentTrack = null )
	{
		var guid = Guid.NewGuid();
		var track = IProjectPropertyTrack.Create( this, guid, name, targetType );

		AddTrackInternal( track, parentTrack );

		return track;
	}

	private void AddTrackInternal( IProjectTrack track, IProjectTrack? parentTrack )
	{
		if ( !_trackDict.TryAdd( track.Id, track ) )
		{
			throw new Exception( "Conflicting track IDs!" );
		}

		((IProjectTrackInternal?)parentTrack)?.AddChild( (IProjectTrackInternal)track );

		_trackList.Add( track );
		_tracksChanged = true;
	}

	internal void RemoveTrackInternal( IProjectTrackInternal projectTrack )
	{
		if ( !_trackList.Remove( projectTrack ) || !_trackDict.Remove( projectTrack.Id ) ) return;

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

		foreach ( var track in _trackList )
		{
			if ( track.Parent is null )
			{
				_rootTrackList.Add( track );
			}
		}
	}
}
