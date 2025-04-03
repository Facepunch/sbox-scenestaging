using Sandbox.MovieMaker;
using System.Linq;
using System.Reflection;

namespace Editor.MovieMaker;

#nullable enable

public partial class MovieEditor : Widget
{
	public Session? Session { get; private set; }

	public ListPanel? ListPanel { get; private set; }
	public DopeSheetPanel? DopeSheetPanel { get; private set; }

	public MovieEditor( Widget parent ) : base( parent )
	{
		Layout = Layout.Column();
		FocusMode = FocusMode.TabOrClickOrWheel;

		UpdateEditorContext();

		if ( Session is null )
		{
			CreateStartupHelper();
		}
	}

	public void Initialize( Session session )
	{
		Session = session;

		if ( Session.Parent is null )
		{
			Session.Player.Clip = Session.Project;
		}

		Layout.Clear( true );

		var splitter = new Splitter( this );

		Layout.Add( splitter );

		ListPanel = new ListPanel( this, Session );
		DopeSheetPanel = new DopeSheetPanel( this, Session );

		splitter.AddWidget( ListPanel );
		splitter.AddWidget( DopeSheetPanel );

		splitter.SetCollapsible( 0, false );
		splitter.SetStretch( 0, 1 );
		splitter.SetCollapsible( 1, false );
		splitter.SetStretch( 1, 3 );

		Session.RestoreFromCookies();
	}

	void CloseSession()
	{
		Layout.Clear( true );

		if ( Session is { } session )
		{
			session.Player.Clip = session.Player.Resource?.Compiled;
		}

		Session = null;
		ListPanel = null;
		DopeSheetPanel = null;

		CreateStartupHelper();
	}

	void CreateStartupHelper()
	{
		var row = Layout.AddRow();

		row.AddStretchCell();

		var col = row.AddColumn();
		col.AddStretchCell();

		col.Add( new Label( "Create a Timeline Player component to get started. The\nTimeline Player is responsible for playing the clip in game." ) );
		col.AddSpacingCell( 32 );

		var button = col.Add( new Button.Primary( "Create Player Component", "add_circle" ) );
		button.Clicked = CreateNew;


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
		Initialize( new Session( this, player ) );
	}

	public void EnterSequence( MovieResource resource, MovieTransform transform, MovieTimeRange timeRange )
	{
		var timeOffset = transform.Inverse * Session!.TimeOffset;
		var pixelsPerSecond = (float)(transform.Inverse.Scale.FrequencyScale * Session.PixelsPerSecond);

		Session!.SetEditMode( null );
		Initialize( new Session( Session!, resource, transform, timeRange ) );

		Session.SetView( timeOffset, pixelsPerSecond );
	}

	public void ExitSequence()
	{
		if ( Session?.Parent is { } parent )
		{
			var timeOffset = Session.SequenceTransform * Session!.TimeOffset;
			var pixelsPerSecond = (float)(Session.SequenceTransform.Scale.FrequencyScale * Session.PixelsPerSecond);

			var resource = Session.Resource;

			Session.Save();
			Initialize( parent );

			Session.SetView( timeOffset, pixelsPerSecond );

			if ( resource is MovieResource movieResource )
			{
				Session.Project.RefreshSequenceTracks( movieResource );
			}
		}
	}

	public void CreateNew()
	{
		using ( SceneEditorSession.Active.Scene.Push() )
		{
			var go = new GameObject( true, "New Timeline Player" );
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

	public void SwitchResource( MovieResource resource )
	{
		if ( Session?.Root.Resource == resource ) return;

		if ( Session is { Resource: EmbeddedMovieResource, Project.IsEmpty: false } )
		{
			Dialog.AskConfirm( () =>
			{
				ConfirmedSwitchResource( resource );
			}, question: "Switching to a clip resource will cause your embedded clip to be lost. Are you sure?" );
		}
		else
		{
			ConfirmedSwitchResource( resource );
		}
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
