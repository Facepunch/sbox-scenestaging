using Sandbox.MovieMaker;
using Sandbox.UI;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Editor.MovieMaker;

#nullable enable

public readonly record struct SessionContext( Session Parent,MovieTransform Transform, MovieTimeRange TimeRange );

/// <summary>
/// Centralizes the current state of a moviemaker editor session
/// </summary>
public sealed partial class Session
{
	public MovieProject Project { get; }

	public MovieEditor Editor { get; private set; }
	public MoviePlayer Player { get; private set; }
	public SessionContext? Context { get; private set; }

	public Session? Parent => Context?.Parent;

	/// <summary>
	/// If this session has a <see cref="Context"/>, how do we transform from this session's timeline to the parent's?
	/// </summary>
	public MovieTransform SequenceTransform => Context?.Transform ?? MovieTransform.Identity;

	/// <summary>
	/// If this session has a <see cref="Context"/>, what time range from this session is visible in the parent?
	/// </summary>
	public MovieTimeRange? SequenceTimeRange => Context?.TimeRange;

	/// <summary>
	/// When previewing playback, what time range to loop within.
	/// </summary>
	public MovieTimeRange? LoopTimeRange { get; set; }

	public Session Root => Context?.Parent.Root ?? this;
	public IMovieResource Resource { get; }

	public string Title => Resource is MovieResource res
		? res.ResourceName.ToTitleCase()
		: Player.GameObject.Name;

	private int _frameRate = 10;
	private bool _frameSnap;
	private bool _objectSnap;
	private MovieTime _timeOffset;
	private float _pixelsPerSecond;

	public bool IsEditorScene => Player.Scene?.IsEditor ?? true;
	public TrackBinder Binder => Player.Binder;

	public int FrameRate
	{
		get => _frameRate;
		set => _frameRate = Cookies.FrameRate = value;
	}

	public bool FrameSnap
	{
		get => _frameSnap;
		set => _frameSnap = Cookies.FrameSnap = value;
	}

	public bool ObjectSnap
	{
		get => _objectSnap;
		set => _objectSnap = Cookies.ObjectSnap = value;
	}

	public MovieTime TimeOffset
	{
		get => _timeOffset;
		private set => _timeOffset = Cookies.TimeOffset = MovieTime.Max( value, default );
	}

	public float PixelsPerSecond
	{
		get => _pixelsPerSecond;
		private set => _pixelsPerSecond = Cookies.PixelsPerSecond = value;
	}

	private MovieTime _playheadTime;
	private MovieTime? _previewTime;

	/// <summary>
	/// Current time being edited. In play mode, this is the current playback time.
	/// </summary>
	public MovieTime PlayheadTime
	{
		get => _playheadTime;
		set
		{
			value = MovieTime.Max( value, MovieTime.Zero );

			if ( PlayheadTime == value ) return;

			_playheadTime = value;
			PlayheadChanged?.Invoke( value );

			if ( IsEditorScene )
			{
				ApplyFrame( value );
			}
			else
			{
				_applyNextFrame = false;
				_lastPlayerPosition = null;

				Player.Position = value;
			}
		}
	}

	/// <summary>
	/// What time are we previewing (when holding shift and moving mouse over timeline).
	/// </summary>
	public MovieTime? PreviewTime
	{
		get => _previewTime;
		set
		{
			if ( value is { } time )
			{
				time = time.Clamp( (MovieTime.Zero, Project.Duration) );

				if ( PreviewTime == time ) return;

				_previewTime = time;
				PreviewChanged?.Invoke( time );

				ApplyFrame( time );
			}
			else if ( PreviewTime is not null )
			{
				_previewTime = null;
				PreviewChanged?.Invoke( null );

				ApplyFrame( PlayheadTime );
			}
		}
	}

	public event Action<MovieTime>? PlayheadChanged;
	public event Action<MovieTime?>? PreviewChanged;

	public bool HasUnsavedChanges { get; private set; }

	public EditMode? EditMode { get; private set; }

	private SmoothDeltaFloat _smoothZoom = new () { Value = 100.0f, Target = 100.0f, SmoothTime = 0.3f };
	private SmoothDeltaFloat _smoothPan = new () { Value = 0.0f, Target = 0f, SmoothTime = 0.3f };

	public MovieTimeRange VisibleTimeRange
	{
		get
		{
			var minTime = PixelsToTime( 0f ) + TimeOffset;
			var maxTime = PixelsToTime( Editor.TimelinePanel!.Width ) + TimeOffset;

			return new (minTime, maxTime);
		}
	}

	/// <summary>
	/// Invoked when the view pans or changes scale.
	/// </summary>
	public event Action? ViewChanged;

	public Session( IMovieResource? resource )
	{
		if ( resource is null )
		{
			Log.Info( $"Creating new embedded!" );
		}

		Resource = resource ?? new EmbeddedMovieResource();
		Project = LoadProject( Resource );

		History = new SessionHistory( this );
	}

	/// <summary>
	/// Called when a <see cref="MovieEditor"/> is switching to this session.
	/// </summary>
	internal void Initialize( MovieEditor editor, MoviePlayer player, SessionContext? context )
	{
		Editor = editor;
		Player = player;
		Context = context;

		Player.Resource = Root.Resource;
		Player.Clip = Root.Project;

		if ( context is { TimeRange: var range } )
		{
			LoopTimeRange = range;
		}
	}

	internal void Activate()
	{
		RestoreFromCookies();
		History.Initialize();
	}

	internal void Deactivate()
	{
		SetEditMode( (EditModeType?)null );

		if ( Player.Clip == Project )
		{
			Player.Clip = Player.Resource?.Compiled;
		}
	}

	private static MovieProject LoadProject( IMovieResource resource )
	{
		// Try to load from Resource.EditorData

		if ( LoadEditorData( resource ) is { } node )
		{
			return node.Deserialize<MovieProject>( EditorJsonOptions )!;
		}

		// Try to create a project from compiled clip

		if ( resource.Compiled is { } compiled )
		{
			return new MovieProject( compiled );
		}

		// Fall back to an empty project

		return new MovieProject();
	}

	private static JsonNode? LoadEditorData( IMovieResource resource )
	{
		if ( resource.EditorData is { } editorData )
		{
			return editorData;
		}

		if ( resource is not MovieResource diskResource ) return null;

		// resource might be the .movie_c, which doesn't contain the project.

		var asset = AssetSystem.FindByPath( diskResource.ResourcePath );
		var sourcePath = asset?.GetSourceFile( true );

		if ( !File.Exists( sourcePath ) ) return null;

		var resourceNode = JsonSerializer.Deserialize<JsonNode>( File.ReadAllText( sourcePath ) );

		return resourceNode?[nameof(IMovieResource.EditorData)];

	}

	internal bool SetEditMode<T>() => SetEditMode( typeof(T) );

	internal bool SetEditMode( Type type )
	{
		if ( type.IsInstanceOfType( EditMode ) ) return true;

		return SetEditMode( new EditModeType( EditorTypeLibrary.GetType( type ) ) );
	}

	internal bool SetEditMode( EditModeType? type )
	{
		if ( type?.IsMatchingType( EditMode ) ?? EditMode is null ) return EditMode is not null;

		IsRecording = false;

		EditMode?.Disable();

		Editor.TimelinePanel!.ToolBar.Reset();

		EditMode = type?.Create();
		EditMode?.Enable( this );

		if ( type is not null )
		{
			Cookies.EditMode = type;
		}

		return EditMode is not null;
	}

	public MovieTime ScenePositionToTime( Vector2 scenePos, SnapOptions? options = null )
	{
		var optionsOrDefault = options ?? new SnapOptions( SnapFlag.None );

		var time = PixelsToTime( scenePos.x );
		var snapHelper = new TimeSnapHelper( time, PixelsToTime( 16f ), optionsOrDefault );

		GetSnapTimes( ref snapHelper );

		foreach ( var offset in optionsOrDefault.SnapOffsets )
		{
			var offsetHelper = new TimeSnapHelper( time + offset, snapHelper.MaxSnap, optionsOrDefault );

			GetSnapTimes( ref offsetHelper );

			snapHelper.Add( offsetHelper );
		}

		return MovieTime.Max( snapHelper.BestTime, MovieTime.Zero );
	}

	private void GetSnapTimes( ref TimeSnapHelper snapHelper )
	{
		if ( FrameSnap )
		{
			var oneFrame = MovieTime.FromFrames( 1, FrameRate );

			snapHelper.Add( SnapFlag.Frame, snapHelper.Time.Round( oneFrame ), -3, force: true );
		}

		if ( ObjectSnap )
		{
			snapHelper.Add( SnapFlag.Playhead, PlayheadTime );

			EditMode?.GetSnapTimes( ref snapHelper );
			Editor.TimelinePanel?.Timeline.GetSnapTimes( ref snapHelper );
		}
	}

	public MovieTime PixelsToTime( float pixels )
	{
		return MovieTime.FromSeconds( pixels / PixelsPerSecond );
	}

	public float TimeToPixels( MovieTime time )
	{
		return (float)(time.TotalSeconds * PixelsPerSecond);
	}

	public void ScrollBy( float x, bool smooth )
	{
		if ( x == 0 )
			return;

		var time = PixelsToTime( x );

		_smoothPan.Target -= (float)time.TotalSeconds;
		_smoothPan.Target = Math.Max( 0f, _smoothPan.Target );

		if ( !smooth )
		{
			_smoothPan.Value = _smoothPan.Target;
			_smoothPan.Velocity = 0;
			TimeOffset = MovieTime.FromSeconds( _smoothPan.Target );
		}

		ViewChanged?.Invoke();
	}

	public void SetView( MovieTime timeOffset, float pixelsPerSecond )
	{
		_timeOffset = MovieTime.Max( timeOffset, default );
		_pixelsPerSecond = pixelsPerSecond;

		_smoothPan.Target = _smoothPan.Value = (float)TimeOffset.TotalSeconds;
		_smoothZoom.Target = _smoothZoom.Value = PixelsPerSecond;

		ViewChanged?.Invoke();
	}

	public bool Frame()
	{
		TrackFrame();
		PlaybackFrame();

		if ( _smoothZoom.Update( RealTime.Delta ) )
		{
			var d = TimeToPixels( TimeOffset ) - TimeToPixels( _zoomOrigin );

			PixelsPerSecond = _smoothZoom.Value;
			PixelsPerSecond = PixelsPerSecond.Clamp( 5, 1024 );

			var nd = TimeToPixels( TimeOffset ) - TimeToPixels( _zoomOrigin );
			ScrollBy( nd - d, false );
		}

		if ( _smoothPan.Update( RealTime.Delta ) )
		{
			TimeOffset = MovieTime.FromSeconds( _smoothPan.Value );
			ViewChanged?.Invoke();
		}

		EditMode?.Frame();

		if ( _applyNextFrame )
		{
			ApplyFrame( PreviewTime ?? PlayheadTime );
		}

		return true;
	}

	private MovieTime _zoomOrigin;

	internal void Zoom( float v, MovieTime origin )
	{
		_zoomOrigin = origin;

		_smoothZoom.Target = _smoothZoom.Target += (v * _smoothZoom.Target) * 0.01f;
		_smoothZoom.Target = _smoothZoom.Target.Clamp( 5, 1024 );
	}

	internal void ClipModified()
	{
		if ( Resource is EmbeddedMovieResource )
		{
			Player.Scene.Editor.HasUnsavedChanges = true;
			return;
		}

		HasUnsavedChanges = true;
	}

	public void Save()
	{
		HasUnsavedChanges = false;

		Resource.EditorData = Project.Serialize();
		Resource.Compiled = Project.Compile();

		// If we're embedded, save the scene

		if ( Resource is EmbeddedMovieResource )
		{
			Player.Scene.Editor.Save( false );
			return;
		}

		// If we're referencing a .movie resource, save it to disk

		if ( Resource is not MovieResource resource )
		{
			return;
		}

		if ( AssetSystem.FindByPath( resource.ResourcePath ) is { } asset )
		{
			asset.SaveToDisk( resource );
		}
	}

	public void Undo()
	{
		if ( History.Undo() )
		{
			EditorUtility.PlayRawSound( "sounds/editor/success.wav" );
		}
	}

	public void Redo()
	{
		if ( History.Redo() )
		{
			EditorUtility.PlayRawSound( "sounds/editor/success.wav" );
		}
	}

	public void DispatchViewChanged()
	{
		ViewChanged?.Invoke();
		EditMode?.ViewChanged( Editor.TimelinePanel!.Timeline.VisibleRect );
	}

	public static float GetGizmoAlpha( MovieTime time, MovieTimeRange range )
	{
		var diff = (time * 2 - (range.Start + range.End)).Absolute;
		var fraction = diff.TotalSeconds / range.Duration.TotalSeconds;

		return Math.Clamp( 2f - (float)fraction * 2f, 0f, 1f );
	}

	public void DrawGizmos()
	{
		var selectedTrackView = TrackList.SelectedTracks.FirstOrDefault();

		if ( selectedTrackView is null ) return;
		if ( selectedTrackView.TransformTrack is not { } transformTrack ) return;

		using var rootScope = Gizmo.Scope( "MovieMaker" );

		Gizmo.Draw.IgnoreDepth = true;

		var timeRange = new MovieTimeRange( PlayheadTime - 5d, PlayheadTime + 5d );
		var clampedTimeRange = timeRange.Clamp( (0d, Project.Duration) );

		EditMode?.DrawGizmos( selectedTrackView, timeRange );

		var timeScale = MovieTimeScale.FromDurationScale( TimeScale );

		(timeScale * MovieTime.FromSeconds( RealTime.Now )).GetFrameIndex( 1d, out var timeOffset );

		for ( var baseTime = clampedTimeRange.Start.Floor( 1d ); baseTime < clampedTimeRange.End; baseTime += 1d )
		{
			var t = baseTime + timeOffset;

			if ( !transformTrack.TryGetValue( t, out var transform ) ) continue;

			var dist = Gizmo.Camera.Ortho ? Gizmo.Camera.OrthoHeight : Gizmo.CameraTransform.Position.Distance( transform.Position );
			var scale = GetGizmoAlpha( t, timeRange ) * dist / 256f;

			var length = 16f * scale;
			var arrowLength = 3f * scale;
			var arrowWidth = 1f * scale;

			Gizmo.Draw.Color = Theme.Red;
			Gizmo.Draw.Arrow( transform.Position, transform.Position + transform.Rotation * Vector3.Forward * length, arrowLength, arrowWidth );

			Gizmo.Draw.Color = Theme.Green;
			Gizmo.Draw.Arrow( transform.Position, transform.Position + transform.Rotation * Vector3.Right * length, arrowLength, arrowWidth );

			Gizmo.Draw.Color = Theme.Blue;
			Gizmo.Draw.Arrow( transform.Position, transform.Position + transform.Rotation * Vector3.Up * length, arrowLength, arrowWidth );
		}
	}

	public bool CanReferenceMovie( MovieResource resource )
	{
		var references = new HashSet<MovieResource>();
		var refQueue = new Queue<MovieResource>();

		references.Add( resource );
		refQueue.Enqueue( resource );

		while ( refQueue.TryDequeue( out var next ) )
		{
			var refs = next.EditorData?["References"]?.Deserialize<ImmutableHashSet<string>>()
				?? ImmutableHashSet<string>.Empty;

			foreach ( var moviePath in refs )
			{
				if ( ResourceLibrary.Get<MovieResource>( moviePath ) is not { } reference )
				{
					continue;
				}

				if ( references.Add( reference ) )
				{
					refQueue.Enqueue( reference );
				}
			}
		}

		return CanReferenceMovieCore( references );
	}

	private bool CanReferenceMovieCore( IReadOnlySet<MovieResource> references )
	{
		// Don't allow cyclic references!

		if ( references.Contains( Resource ) ) return false;

		return Parent?.CanReferenceMovieCore( references ) ?? true;
	}

	private void ImportMovie( MovieResource resource, MovieTime time = default )
	{
		if ( !CanReferenceMovie( resource ) ) return;

		using var historyScope = History.Push( $"Import {resource.ResourceName.ToTitleCase()}" );

		var track = GetOrCreateTrack( resource );

		var start = time;
		var end = time + resource.GetCompiled().Duration;

		track.AddBlock( (start, end), new MovieTransform( -start ), resource );

		if ( track.Blocks.Count == 1 )
		{
			TrackList.Update();
		}
		else
		{
			TrackList.Find( track )?.MarkValueChanged();
		}
	}

	public void CreateImportMenu( Menu parent, MovieTime time = default )
	{
		var movies = ResourceLibrary.GetAll<MovieResource>().ToArray();
		var resourceIcon = typeof( MovieResource ).GetCustomAttribute<GameResourceAttribute>()!.Icon;

		var importMenu = parent.AddMenu( "Import Movie", "sim_card_download" );

		importMenu.AddOptions( movies.Where( CanReferenceMovie ),
			x => $"{x.ResourcePath}:{resourceIcon}",
			x => ImportMovie( x, time ) );
	}
}
