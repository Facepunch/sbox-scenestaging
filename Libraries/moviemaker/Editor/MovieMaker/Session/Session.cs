using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Sandbox.MovieMaker;
using Sandbox.UI;

namespace Editor.MovieMaker;

#nullable enable

/// <summary>
/// Centralizes the current state of a moviemaker editor session
/// </summary>
public sealed partial class Session
{
	public MovieEditor Editor { get; }
	public MoviePlayer Player { get; }
	public MovieProject Project { get; }
	public Session? Parent { get; }

	/// <summary>
	/// If this session has a <see cref="Parent"/>, how do we transform from this session's timeline to the parent's?
	/// </summary>
	public MovieTransform SequenceTransform { get; }

	/// <summary>
	/// If this session has a <see cref="Parent"/>, what time range from this session is visible in the parent?
	/// </summary>
	public MovieTimeRange? SequenceTimeRange { get; }

	public Session Root => Parent?.Root ?? this;
	public IMovieResource Resource { get; }

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
		private set => _timeOffset = Cookies.TimeOffset = value;
	}

	public float PixelsPerSecond
	{
		get => _pixelsPerSecond;
		private set => _pixelsPerSecond = Cookies.PixelsPerSecond = value;
	}

	/// <summary>
	/// Current time being edited. In play mode, this is the current playback time.
	/// </summary>
	public MovieTime CurrentPointer { get; private set; }

	/// <summary>
	/// What time are we previewing (when holding shift and moving mouse over timeline).
	/// </summary>
	public MovieTime? PreviewPointer { get; private set; }

	public bool HasUnsavedChanges { get; private set; }

	public EditMode? EditMode { get; private set; }

	SmoothDeltaFloat SmoothZoom = new SmoothDeltaFloat { Value = 100.0f, Target = 100.0f, SmoothTime = 0.3f };
	SmoothDeltaFloat SmoothPan = new SmoothDeltaFloat { Value = 0.0f, Target = 0f, SmoothTime = 0.3f };

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

	public Session( MovieEditor editor, MoviePlayer player )
	{
		if ( player.Resource is null )
		{
			Log.Info( $"Creating new embedded!" );
		}

		Editor = editor;
		Player = player;
		Parent = null;
		Resource = player.Resource ??= new EmbeddedMovieResource();
		SequenceTransform = MovieTransform.Identity;
		SequenceTimeRange = null;
		Project = LoadProject( Resource );

		History = new SessionHistory( this );
	}

	public Session( Session parent, MovieResource resource, MovieTransform transform, MovieTimeRange timeRange )
	{
		Editor = parent.Editor;
		Player = parent.Player;
		Parent = parent;
		Resource = resource;
		SequenceTransform = transform;
		SequenceTimeRange = timeRange;
		Project = LoadProject( Resource );

		History = new SessionHistory( this );
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
			snapHelper.Add( SnapFlag.PlayHead, CurrentPointer );

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

		SmoothPan.Target -= (float)time.TotalSeconds;

		if ( !smooth )
		{
			SmoothPan.Value = SmoothPan.Target;
			SmoothPan.Velocity = 0;
			TimeOffset = MovieTime.FromSeconds( SmoothPan.Target );
		}

		ViewChanged?.Invoke();
	}

	public event Action<MovieTime>? PointerChanged;
	public event Action<MovieTime?>? PreviewChanged;

	public void SetCurrentPointer( MovieTime time )
	{
		time = MovieTime.Max( time, MovieTime.Zero );

		if ( CurrentPointer == time ) return;

		CurrentPointer = time;
		PointerChanged?.Invoke( CurrentPointer );

		if ( IsEditorScene )
		{
			ApplyFrame( CurrentPointer );
		}
		else
		{
			_applyNextFrame = false;
			_lastPlayerPosition = null;

			Player.Position = CurrentPointer;
		}
	}

	public void SetPreviewPointer( MovieTime time )
	{
		PreviewPointer = MovieTime.Max( time, MovieTime.Zero );
		PreviewChanged?.Invoke( PreviewPointer );

		ApplyFrame( PreviewPointer.Value );
	}

	public void ClearPreviewPointer()
	{
		PreviewPointer = null;

		PreviewChanged?.Invoke( PreviewPointer );

		ApplyFrame( CurrentPointer );
	}

	public void SetView( MovieTime timeOffset, float pixelsPerSecond )
	{
		_timeOffset = timeOffset;
		_pixelsPerSecond = pixelsPerSecond;

		SmoothPan.Target = SmoothPan.Value = (float)TimeOffset.TotalSeconds;
		SmoothZoom.Target = SmoothZoom.Value = PixelsPerSecond;

		ViewChanged?.Invoke();
	}

	public bool Frame()
	{
		TrackFrame();
		PlaybackFrame();

		if ( SmoothZoom.Update( RealTime.Delta ) )
		{
			var d = TimeToPixels( TimeOffset ) - TimeToPixels( _zoomOrigin );

			PixelsPerSecond = SmoothZoom.Value;
			PixelsPerSecond = PixelsPerSecond.Clamp( 5, 1024 );

			var nd = TimeToPixels( TimeOffset ) - TimeToPixels( _zoomOrigin );
			ScrollBy( nd - d, false );
		}

		if ( SmoothPan.Update( RealTime.Delta ) )
		{
			TimeOffset = MovieTime.FromSeconds( SmoothPan.Value );
			ViewChanged?.Invoke();
		}

		EditMode?.Frame();

		if ( _applyNextFrame )
		{
			ApplyFrame( PreviewPointer ?? CurrentPointer );
		}

		return true;
	}

	private MovieTime _zoomOrigin;

	internal void Zoom( float v, MovieTime origin )
	{
		_zoomOrigin = origin;

		SmoothZoom.Target = SmoothZoom.Target += (v * SmoothZoom.Target) * 0.01f;
		SmoothZoom.Target = SmoothZoom.Target.Clamp( 5, 1024 );
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

		var timeRange = new MovieTimeRange( CurrentPointer - 5d, CurrentPointer + 5d );
		var clampedTimeRange = timeRange.Clamp( (0d, Project.Duration) );

		EditMode?.DrawGizmos( selectedTrackView, timeRange );

		var timeScale = MovieTimeScale.FromDurationScale( TimeScale );

		(timeScale * MovieTime.FromSeconds( RealTime.Now )).GetFrameIndex( 1d, out var timeOffset );

		for ( var baseTime = clampedTimeRange.Start.Floor( 1d ); baseTime < clampedTimeRange.End; baseTime += 1d )
		{
			var t = baseTime + timeOffset;

			if ( !transformTrack.TryGetValue( t, out var transform ) ) continue;

			var dist = Gizmo.CameraTransform.Position.Distance( transform.Position );
			var scale = GetGizmoAlpha( t, timeRange ) * dist / 512f;

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
			var refs = next.EditorData?["References"]?.Deserialize<ImmutableHashSet<MovieResource>>()
				?? ImmutableHashSet<MovieResource>.Empty;

			foreach ( var @ref in refs )
			{
				if ( references.Add( @ref ) )
				{
					refQueue.Enqueue( @ref );
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
}
