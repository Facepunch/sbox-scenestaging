using System.Collections.Immutable;
using Sandbox.MovieMaker;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Editor.MovieMaker;

#nullable enable

public partial class MovieEditor : Widget, IHotloadManaged
{
	private static readonly ConditionalWeakTable<MovieEditor, object?> _editors = new();

	public static IEnumerable<Session> ActiveSessions => _editors
		.Select( x => x.Key )
		.Where( x => x.IsValid() )
		.Where( x => x.Session is not null )
		.Select( x => x.Session! );

	// We want sessions to survive entering play mode etc., so identify by MoviePlayer component ID
	// and which resource they're editing. A null resource means embedded.

	public readonly record struct SessionKey( Guid PlayerId, string? ResourcePath );

	private readonly Dictionary<SessionKey, Session> _sessions;

	public Session? Session { get; private set; }

	public ListPanel? ListPanel { get; private set; }
	public TimelinePanel? TimelinePanel { get; private set; }

	public MovieEditor( Widget parent, IReadOnlyDictionary<SessionKey, Session>? sessions = null ) : base( parent )
	{
		_editors.Add( this, null );
		_sessions = sessions?.ToDictionary() ?? new Dictionary<SessionKey, Session>();

		Layout = Layout.Column();
		FocusMode = FocusMode.TabOrClickOrWheel;

		MinimumSize = new Vector2( 800, 300 );

		UpdateEditorContext();

		if ( Session is null )
		{
			CreateStartupHelper();
		}
	}

	internal IReadOnlyDictionary<SessionKey, Session> Sessions => _sessions.ToImmutableDictionary();

	public override void OnDestroyed()
	{
		_editors.Remove( this );

		base.OnDestroyed();
	}

	private void Initialize( MoviePlayer player, IMovieResource? resource, SessionContext? context )
	{
		Session?.Deactivate();

		var key = new SessionKey( player.Id, (resource as MovieResource)?.ResourcePath );

		if ( _sessions.TryGetValue( key, out var session ) )
		{
			Session = session;
		}
		else
		{
			Session = _sessions[key] = new Session( resource );
		}

		Session.Initialize( this, player, context );

		Layout.Clear( true );

		var splitter = new Splitter( this );

		Layout.Add( splitter );

		ListPanel = new ListPanel( this, Session );
		TimelinePanel = new TimelinePanel( this, Session );

		splitter.AddWidget( ListPanel );
		splitter.AddWidget( TimelinePanel );

		splitter.SetCollapsible( 0, false );
		splitter.SetStretch( 0, 1 );
		splitter.SetCollapsible( 1, false );
		splitter.SetStretch( 1, 3 );

		Session.Activate();
	}

	void CloseSession()
	{
		Layout.Clear( true );

		Session?.Deactivate();
		Session = null;

		ListPanel = null;
		TimelinePanel = null;

		CreateStartupHelper();
	}

	void CreateStartupHelper()
	{
		var row = Layout.AddRow();

		row.AddStretchCell();

		var col = row.AddColumn();
		col.AddStretchCell();

		col.Add( new Label( "Create a Movie Player component to get started. The\nMovie Player is responsible for playing the clip in-game." ) );
		col.AddSpacingCell( 32 );

		var button = col.Add( new Button.Primary( "Create Player Component", "add_circle" ) );
		button.Clicked = CreateNewPlayer;


		col.AddStretchCell();

		row.AddStretchCell();
	}

	[EditorEvent.Frame]
	public void Frame()
	{
		UpdateEditorContext();

		Session?.Frame();
	}

	[Shortcut( "timeline.playtoggle", "Space", ShortcutType.Window )]
	public void PlayToggle()
	{
		if ( Session is null )
			return;

		Session.IsPlaying = !Session.IsPlaying;
	}

	[Shortcut( "editor.save", "CTRL+S" )]
	public void OnSave()
	{
		Session?.Save();
	}

	[Shortcut( "timeline.selectall", "CTRL+A" )]
	public void OnSelectAll()
	{
		Session?.EditMode?.SelectAll();
	}

	[Shortcut( "timeline.cut", "CTRL+X" )]
	public void OnCut()
	{
		Session?.EditMode?.Cut();
	}

	[Shortcut( "timeline.copy", "CTRL+C" )]
	public void OnCopy()
	{
		Session?.EditMode?.Copy();
	}

	[Shortcut( "timeline.paste", "CTRL+V" )]
	public void OnPaste()
	{
		Session?.EditMode?.Paste();
	}

	[Shortcut( "timeline.backspace", "BACKSPACE" )]
	public void OnBackspace()
	{
		Session?.EditMode?.Backspace();
	}

	[Shortcut( "timeline.delete", "DEL" )]
	public void OnDelete()
	{
		Session?.EditMode?.Delete();
	}

	[Shortcut( "timeline.insert", "TAB" )]
	public void OnInsert()
	{
		Session?.EditMode?.Insert();
	}

	[Shortcut( "editor.undo", "CTRL+Z" )]
	public void OnUndo()
	{
		Session?.Undo();
	}

	[Shortcut( "editor.redo", "CTRL+Y" )]
	public void OnRedo()
	{
		Session?.Redo();
	}

	/// <summary>
	/// Look for any clips we can edit. If the clip we're editing has gone - stop editing it.
	/// </summary>
	void UpdateEditorContext()
	{
		if ( SceneEditorSession.Active?.Scene is not { } scene ) return;

		// The current session exists
		if ( Session is { } session )
		{
			// Whatever we were editing doesn't exist anymore!
			if ( !session.Player.IsValid || session.Player.Scene != scene )
			{
				CloseSession();
			}
		}

		// session is null, lets load the first player
		if ( Session is null )
		{
			if ( scene.GetAllComponents<MoviePlayer>().FirstOrDefault() is { } player )
			{
				Switch( player );
			}
		}
	}

	public void Switch( MoviePlayer player )
	{
		Initialize( player, player.Resource, null );
	}

	public void EnterSequence( MovieResource resource, MovieTransform transform, MovieTimeRange timeRange )
	{
		var timeOffset = transform.Inverse * Session!.TimeOffset;
		var pixelsPerSecond = (float)(transform.Inverse.Scale.FrequencyScale * Session.PixelsPerSecond);

		Initialize( Session.Player, resource, new SessionContext( Session, transform, timeRange ) );

		Session.SetView( timeOffset, pixelsPerSecond );
	}

	public void ExitSequence()
	{
		if ( Session?.Context is not { } context ) return;

		var timeOffset = Session.SequenceTransform * Session!.TimeOffset;
		var pixelsPerSecond = (float)(Session.SequenceTransform.Scale.FrequencyScale * Session.PixelsPerSecond);

		var resource = Session.Resource;

		Session.Save();
		Initialize( Session.Player, context.Parent.Resource, context.Parent.Context );

		Session.SetView( timeOffset, pixelsPerSecond );

		if ( resource is MovieResource movieResource )
		{
			Session.Project.RefreshSequenceTracks( movieResource );
		}
	}

	public void CreateNewPlayer()
	{
		using ( SceneEditorSession.Active.Scene.Push() )
		{
			var go = new GameObject( true, "New Movie Player" );
			go.Components.Create<MoviePlayer>();

			SceneEditorSession.Active.Selection.Set( go );
		}
	}

	public void SwitchToEmbedded()
	{
		if ( Session!.Resource is EmbeddedMovieResource ) return;

		Session.Player.Resource = new EmbeddedMovieResource
		{
			Compiled = Session.Resource.Compiled,
			EditorData = Session.Project.Serialize()
		};

		Switch( Session.Player );
	}

	public void SwitchToNewEmbedded()
	{
		if ( Session is not { } session ) return;

		if ( session is { Resource: EmbeddedMovieResource, Project.IsEmpty: false } )
		{
			Dialog.AskConfirm( ConfirmSwitchToNewEmbedded, question: "The current embedded clip will be lost. Are you sure?" );
			return;
		}

		if ( session is { Resource: MovieResource resource, HasUnsavedChanges: true } )
		{
			Dialog.AskConfirm( () =>
			{
				session.Save();
				ConfirmSwitchToNewEmbedded();
			}, ConfirmSwitchToNewEmbedded, question: $"Save unsaved changes to {resource.ResourceName}.movie?", okay: "Save", cancel: "Don't Save" );
			return;
		}

		ConfirmSwitchToNewEmbedded();
	}

	private void ConfirmSwitchToNewEmbedded()
	{
		if ( Session is not { } session ) return;

		var player = session.Player;

		player.Resource = new EmbeddedMovieResource();

		Switch( player );
	}

	public void SwitchResource( MovieResource resource )
	{
		if ( Session is not { } session ) return;
		if ( session.Root.Resource == resource ) return;

		if ( session is { Resource: EmbeddedMovieResource, Project.IsEmpty: false } )
		{
			Dialog.AskConfirm( () =>
			{
				ConfirmedSwitchResource( resource );
			}, question: "Switching to a clip resource will cause your embedded clip to be lost. Are you sure?" );
			return;
		}

		if ( session is { Resource: MovieResource unsaved, HasUnsavedChanges: true } )
		{
			Dialog.AskConfirm( () =>
				{
					session.Save();
					ConfirmedSwitchResource( resource );
				}, () => ConfirmedSwitchResource( resource ), question: $"Save unsaved changes to {unsaved.ResourceName}.movie?",
				okay: "Save", cancel: "Don't Save" );
			return;
		}

		ConfirmedSwitchResource( resource );
	}

	private void ConfirmedSwitchResource( MovieResource resource )
	{
		Session!.Player.Resource = resource;

		Switch( Session.Player );
	}

	public void SaveFileAs() => SaveAsDialog( "Save Movie As..",
		() => new MovieResource { Compiled = Session!.Project.Compile(), EditorData = Session.Project.Serialize() },
		ConfirmedSwitchResource );

	public void SaveAsDialog( string title, Func<MovieResource> createResource, Action<MovieResource>? afterSave = null )
	{
		var extension = typeof(MovieResource).GetCustomAttribute<GameResourceAttribute>()!.Extension;

		var fd = new FileDialog( null );
		fd.Title = title;
		fd.Directory = Project.Current.GetAssetsPath();
		fd.DefaultSuffix = $".{extension}";
		fd.SetFindFile();
		fd.SetModeSave();
		fd.SetNameFilter( $"Movie Clip File (*.{extension})" );

		if ( !fd.Execute() )
			return;

		var sceneAsset = AssetSystem.CreateResource( extension, fd.SelectedFile );
		var file = createResource();

		sceneAsset.SaveToDisk( file );

		afterSave?.Invoke( file );
	}
}
