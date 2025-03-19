using System;
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

	/// <summary>
	/// Set to true when tracks need sorting / root tracks might have changed.
	/// </summary>
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

	public MovieProject()
	{

	}

	/// <summary>
	/// Create a project based on a compiled clip, so that clip can be edited.
	/// </summary>
	internal MovieProject( CompiledClip clip )
	{
		var source = AddSourceClip( clip );

		foreach ( var compiledTrack in clip.Tracks )
		{
			var parentTrack = compiledTrack.Parent is { } parent ? GetTrack( parent ) : null;

			switch ( compiledTrack )
			{
				case ICompiledReferenceTrack refTrack:
				{
					var track = IProjectReferenceTrack.Create( this, refTrack.Id, refTrack.Name, refTrack.TargetType );

					AddTrackInternal( track, parentTrack );
					continue;
				}
				case ICompiledPropertyTrack propertyTrack:
				{
					var track = IProjectPropertyTrack.Create( this, Guid.NewGuid(), propertyTrack.Name, propertyTrack.TargetType );

					foreach ( var block in track.CreateSourceBlocks( source ) )
					{
						track.Add( block );
					}

					AddTrackInternal( track, parentTrack );
					continue;
				}
				default:
					throw new NotImplementedException();
			}
		}
	}

	public IProjectTrack? GetTrack( Guid trackId )
	{
		return _trackDict!.GetValueOrDefault( trackId );
	}

	public IProjectTrack? GetTrack( ITrack track )
	{
		if ( track is IProjectTrack projTrack && projTrack.Project == this )
		{
			return projTrack;
		}

		if ( track is IReferenceTrack refTrack )
		{
			return GetTrack( refTrack.Id );
		}

		return track.Parent is { } parent
			? GetTrack( parent )?.GetChild( track.Name )
			: null;
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

	/// <summary>
	/// Makes sure tracks are sorted / root tracks are correct.
	/// </summary>
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
