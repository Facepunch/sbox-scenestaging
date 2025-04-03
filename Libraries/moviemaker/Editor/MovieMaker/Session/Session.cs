using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Sandbox.MovieMaker;

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
			var maxTime = PixelsToTime( Editor.DopeSheetPanel!.Width ) + TimeOffset;

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
		Project = LoadProject( Resource );

		History = new SessionHistory( this );
	}

	public Session( Session parent, MovieResource resource )
	{
		Editor = parent.Editor;
		Player = parent.Player;
		Parent = parent;
		Resource = resource;
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

	internal void SetEditMode<T>()
	{
		if ( EditMode is T ) return;

		SetEditMode( new EditModeType( EditorTypeLibrary.GetType<T>() ) );
	}

	internal void SetEditMode( EditModeType? type )
	{
		if ( type?.IsMatchingType( EditMode ) ?? EditMode is null ) return;

		IsRecording = false;

		EditMode?.Disable();

		Editor.DopeSheetPanel!.ToolBar.Reset();

		EditMode = type?.Create();
		EditMode?.Enable( this );

		if ( type is not null )
		{
			Cookies.EditMode = type;
		}
	}

	public MovieTime ScenePositionToTime( Vector2 scenePos, SnapFlag ignore = 0, ITrackView? ignoreTrack = null, params MovieTime[] snapOffsets ) 
	{
		var time = PixelsToTime( scenePos.x );
		var snapHelper = new TimeSnapHelper( time, PixelsToTime( 16f ), ignore, ignoreTrack );

		GetSnapTimes( ref snapHelper );

		foreach ( var offset in snapOffsets )
		{
			var offsetHelper = new TimeSnapHelper( time + offset, snapHelper.MaxSnap, snapHelper.Ignore, ignoreTrack );

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
			Editor.DopeSheetPanel?.DopeSheet.GetSnapTimes( ref snapHelper );
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
		if ( SmoothPan.Target < 0 ) SmoothPan.Target = 0;

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
		CurrentPointer = MovieTime.Max( time, MovieTime.Zero );
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
			var d = TimeToPixels( TimeOffset ) - TimeToPixels( CurrentPointer );

			PixelsPerSecond = SmoothZoom.Value;
			PixelsPerSecond = PixelsPerSecond.Clamp( 5, 1024 );

			var nd = TimeToPixels( TimeOffset ) - TimeToPixels( CurrentPointer );
			ScrollBy( nd - d, false );

			ViewChanged?.Invoke();
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

	internal void Zoom( float v )
	{
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
		EditMode?.ViewChanged( Editor.DopeSheetPanel!.DopeSheet.VisibleRect );
	}
}
