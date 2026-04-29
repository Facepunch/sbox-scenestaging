using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Editor;
using Sandbox;

using Button = Editor.Button;
using Label = Editor.Label;
using Checkbox = Editor.Checkbox;
using LineEdit = Editor.LineEdit;

[Dock( "Editor", "Version Control", "account_tree" )]
public partial class LocalControlWidget : Widget
{
	// --- State ---

	private Widget _changesView;
	private Widget _historyView;
	private Widget _noGitView;

	private ScrollArea _changesScroll;
	private ScrollArea _historyScroll;

	private TextEdit _commitMessage;
	private LineEdit _gitRemoteUrl;
	private Button _commitBtn;
	private Button _changesTabBtn;
	private Button _historyTabBtn;

	private bool _showChangesTab = true;
	private bool _needsRefresh = false;
	private bool _gitOperationInProgress = false;
	private FileSystemWatcher _watcher;
	private TimeSince _lastRefresh = 0;

	private class ChangeRow
	{
		public string FilePath;
		public Checkbox Toggle;
	}
	private List<ChangeRow> _trackedRows = new();

	public LocalControlWidget( Widget parent ) : base( parent )
	{
		MinimumSize = new Vector2( 200, 500 );
		Layout = Layout.Column();
		Layout.Margin = 10;
		Layout.Spacing = 8;

		BuildTopBar();
		Layout.AddSeparator();

		_noGitView = new Widget( this );
		_noGitView.Layout = Layout.Column();

		_changesView = new Widget( this );
		_changesView.Layout = Layout.Column();
		_changesView.Visible = false;

		_historyView = new Widget( this );
		_historyView.Layout = Layout.Column();
		_historyView.Visible = false;

		Layout.Add( _noGitView );
		Layout.Add( _changesView );
		Layout.Add( _historyView );

		BuildNoGitView();
		BuildChangesArea();
		BuildHistoryArea();

		SetupAutoRefresh();
		UpdateRootView();

		TryPopulateGitRemoteUrlFromOrigin();
	}

	// --- Top bar ---

	private void BuildTopBar()
	{
		var topBar = Layout.AddRow();
		topBar.Spacing = 5;

		_changesTabBtn = new Button( "📝 Changes" );
		_changesTabBtn.Clicked += () => ShowTab( true );
		topBar.Add( _changesTabBtn );

		_historyTabBtn = new Button( "🕒 History" );
		_historyTabBtn.Clicked += () => ShowTab( false );
		topBar.Add( _historyTabBtn );

		topBar.AddStretchCell();

		var refreshBtn = new Button( string.Empty, "refresh" );
		refreshBtn.ToolTip = "Refresh View";
		refreshBtn.Clicked += OnRefreshClicked;
		topBar.Add( refreshBtn );
	}

	// --- first run view ---

	private void BuildNoGitView()
	{
		_noGitView.Layout.Margin = 16;
		_noGitView.Layout.Spacing = 12;

		_noGitView.Layout.Add( new Label( "<big><b>⚠ Git Not Found</b></big>" ) );

		var desc = new Label(
			"This Library uses Git for version control. Git does not appear to be installed or is not available in your system PATH.\n\n" +
			"Git stores your project history locally — no account or internet connection is required. You can optionally push to GitHub later.",
			_noGitView
		);
		desc.WordWrap = true;
		_noGitView.Layout.Add( desc );

		_noGitView.Layout.AddSeparator();

		_noGitView.Layout.Add( new Label( "<b>To get started:</b>" ) );

		var steps = new Label(
			"1. Download and install Git from  git-scm.com/download/win\n" +
			"2. Restart s&box after installing\n" +
			"3. Click Refresh above — the panel will activate automatically",
			_noGitView
		);
		steps.WordWrap = true;
		_noGitView.Layout.Add( steps );

		_noGitView.Layout.AddSeparator();

		var footer = new Label( "<font color='#888'>Once Git is installed, this panel will let you commit, view history, restore previous versions, and optionally push to GitHub.</font>", _noGitView );
		footer.WordWrap = true;
		_noGitView.Layout.Add( footer );

		_noGitView.Layout.AddStretchCell();
	}

	// --- Changes area ---

	private Label _syncStatusLabel;
	private void BuildChangesArea()
	{
		_changesView.Layout.Add( new Label( "<big><b>Uncommitted Changes:</b></big>" ) );

		_syncStatusLabel = new Label( "<font color='#888'>Checking sync status...</font>", _changesView );
		_changesView.Layout.Add( _syncStatusLabel );

		var spacer1 = new Widget( _changesView );
		spacer1.MinimumSize = new Vector2( 0, 8 );
		_changesView.Layout.Add( spacer1 );

		_changesScroll = new ScrollArea( _changesView );
		_changesView.Layout.Add( _changesScroll );

		_changesView.Layout.AddSeparator();

		var spacer2 = new Widget( _changesView );
		spacer2.MinimumSize = new Vector2( 0, 8 );
		_changesView.Layout.Add( spacer2 );

		_changesView.Layout.Add( new Label( "<big><b>Commit Message:</b></big>" ) );

		var textSpacer = new Widget( _changesView );
		textSpacer.MinimumSize = new Vector2( 0, 4 );
		_changesView.Layout.Add( textSpacer );

		_commitMessage = new TextEdit( _changesView );
		_commitMessage.MinimumSize = new Vector2( 0, 80 );
		_commitMessage.MaximumSize = new Vector2( 9999, 80 );
		_changesView.Layout.Add( _commitMessage );

		_commitBtn = new Button( "Commit", "check_circle" );
		_commitBtn.Clicked += OnCommitClicked;
		_changesView.Layout.Add( _commitBtn );

		var remoteSpacer = new Widget( _changesView );
		remoteSpacer.MinimumSize = new Vector2( 0, 12 );
		_changesView.Layout.Add( remoteSpacer );

		// Remote / push-pull section
		var remoteSection = new Widget( _changesView );
		remoteSection.Layout = Layout.Column();
		remoteSection.Layout.Spacing = 5;
		_changesView.Layout.Add( remoteSection );

		remoteSection.Layout.Add( new Label( "<b>Remote URL (optional):</b>" ) );

		_gitRemoteUrl = new LineEdit();
		_gitRemoteUrl.PlaceholderText = "https://github.com/user/repo.git";
		_gitRemoteUrl.MinimumSize = new Vector2( 0, 24 );
		_gitRemoteUrl.MaximumSize = new Vector2( 9999, 24 );
		remoteSection.Layout.Add( _gitRemoteUrl );

		var spacer = new Widget( remoteSection );
		spacer.MinimumSize = new Vector2( 0, 2 );
		remoteSection.Layout.Add( spacer );

		var row1 = remoteSection.Layout.AddRow();
		row1.Spacing = 5;

		var initBtn = new Button( "Init Git", "create_new_folder" );
		initBtn.ToolTip = "Initialize a new Git repository in this project folder";
		initBtn.Clicked += OnInitGitClicked;
		row1.Add( initBtn, 1 );

		var connectBtn = new Button( "Set Remote", "hub" );
		connectBtn.ToolTip = "Set the remote repository URL for this project";
		connectBtn.Clicked += OnConnectOriginClicked;
		row1.Add( connectBtn, 1 );

		var row2 = remoteSection.Layout.AddRow();
		row2.Spacing = 5;

		var pullBtn = new Button( "Pull", "download" );
		pullBtn.ToolTip = "Pull changes from the remote repository. Make sure to commit or stash your local changes first!";
		pullBtn.Clicked += OnPullClicked;
		row2.Add( pullBtn, 1 );

		var pushBtn = new Button( "Push", "upload" );
		pushBtn.ToolTip = "Push your committed changes to the remote repository.";
		pushBtn.Clicked += OnPushClicked;
		row2.Add( pushBtn, 1 );
	}

	// --- History area ---

	private void BuildHistoryArea()
	{
		_historyView.Layout.Add( new Label( "<big><b>Commit History:</b></big>" ) );

		var spacer = new Widget( _historyView );
		spacer.MinimumSize = new Vector2( 0, 8 );
		_historyView.Layout.Add( spacer );

		_historyScroll = new ScrollArea( _historyView );
		_historyView.Layout.Add( _historyScroll );
	}

	// --- View switching ---

	private void UpdateRootView()
	{
		bool gitReady = IsGitAvailable();

		_noGitView.Visible = !gitReady;
		_changesTabBtn.Enabled = gitReady;
		_historyTabBtn.Enabled = gitReady;

		if ( gitReady )
		{
			ShowTab( _showChangesTab );
		}
		else
		{
			_changesView.Visible = false;
			_historyView.Visible = false;
		}
	}

	private void ShowTab( bool showChanges )
	{
		_showChangesTab = showChanges;
		_changesView.Visible = showChanges;
		_historyView.Visible = !showChanges;
		_noGitView.Visible = false;

		if ( showChanges )
		{
			OnRefreshClicked();
		}
		else
		{
			RefreshHistoryUI();
		}
	}

	// --- Helpers ---

	private void AddChangeItem( Widget parentCanvas, string filePath, string status )
	{
		var row = parentCanvas.Layout.AddRow();
		row.Spacing = 5;

		var cb = new Checkbox { Value = true };
		row.Add( cb );

		var fileLabel = new ClickableLabel( filePath, parentCanvas );
		fileLabel.ToolTip = "Double-click to view diff";
		fileLabel.DoubleClicked += () => OpenDiffWindow( filePath );
		fileLabel.RightClicked += pos => ShowChangeItemContextMenu( filePath, pos );

		row.Add( fileLabel );
		row.AddStretchCell();
		row.Add( new Label( status ) );

		_trackedRows.Add( new ChangeRow { FilePath = filePath, Toggle = cb } );
	}

	private void ShowChangeItemContextMenu( string filePath, Vector2 screenPosition )
	{
		var menu = new ContextMenu( this );
		var normalizedFilePath = filePath.Replace( "\\", "/" ).Trim();

		menu.AddOption( $"Ignore File: {normalizedFilePath}", "block", () =>
		{
			if ( AddToGitIgnore( normalizedFilePath ) )
			{
				OnRefreshClicked();
			}
		} );

		var folderPath = GetFolderIgnorePath( normalizedFilePath );
		if ( !string.IsNullOrWhiteSpace( folderPath ) )
		{
			menu.AddOption( $"Ignore Folder: {folderPath}", "folder_off", () =>
			{
				if ( AddToGitIgnore( folderPath ) )
				{
					OnRefreshClicked();
				}
			} );
		}

		menu.AddOption( "Discard Changes", "history", () =>
		{
			Editor.Dialog.AskConfirm(
				() => PerformDiscard( normalizedFilePath ),
				$"Are you sure you want to permanently discard changes to '{normalizedFilePath}'? \n\nThis cannot be undone.",
				"Discard Changes?"
			);
		} );

		menu.OpenAt( screenPosition, false );
	}

	private void PerformDiscard( string path )
	{
		var (ok, _, err) = RunGitCommand( $"restore {QuoteArg( path )}" );
		if ( ok )
		{
			Log.Info( $"Discarded changes to: {path}" );
			OnRefreshClicked();
		}
		else
		{
			Log.Error( $"Discard failed: {err}" );
		}
	}

	private static string GetFolderIgnorePath( string filePath )
	{
		var dir = Path.GetDirectoryName( filePath )?.Replace( "\\", "/" );
		if ( string.IsNullOrWhiteSpace( dir ) )
		{
			return null;
		}

		return dir.EndsWith( "/" ) ? dir : dir + "/";
	}

	private bool AddToGitIgnore( string entry )
	{
		if ( string.IsNullOrWhiteSpace( entry ) )
		{
			return false;
		}

		var gitIgnorePath = Path.Combine( Project.Current.GetRootPath(), ".gitignore" );
		var normalizedEntry = entry.Replace( "\\", "/" ).Trim();

		var lines = File.Exists( gitIgnorePath )
			? File.ReadAllLines( gitIgnorePath ).ToList()
			: new List<string>();

		if ( lines.Any( x => string.Equals( x.Trim(), normalizedEntry, StringComparison.Ordinal ) ) )
		{
			Log.Info( $"Already in .gitignore: {normalizedEntry}" );
			return false;
		}

		lines.Add( normalizedEntry );
		File.WriteAllLines( gitIgnorePath, lines );
		Log.Info( $"Added to .gitignore: {normalizedEntry}" );
		return true;
	}

	private void OpenDiffWindow( string relativePath )
	{
		string currentPath = Path.Combine( Project.Current.GetRootPath(), relativePath );
		string currentContent = File.Exists( currentPath ) ? File.ReadAllText( currentPath ) : "[File is missing in current workspace]";
		string oldContent = ReadGitFileAtRevision( "HEAD", relativePath );

		var diff = new DiffWindow( relativePath, oldContent, currentContent );
		diff.Show();
	}

	private void RefreshHistoryUI()
	{
		if ( _historyScroll.Canvas != null )
		{
			_historyScroll.Canvas.Destroy();
		}

		var canvas = new Widget( _historyScroll );
		canvas.Layout = Layout.Column();
		_historyScroll.Canvas = canvas;

		RefreshGitHistoryUI( canvas );
	}

	private void OnRefreshClicked()
	{
		if ( !IsGitAvailable() )
		{
			UpdateRootView();
			return;
		}

		if ( _changesScroll.Canvas != null )
		{
			_changesScroll.Canvas.Destroy();
		}

		var canvas = new Widget( _changesScroll );
		canvas.Layout = Layout.Column();
		_changesScroll.Canvas = canvas;

		_trackedRows.Clear();

		RefreshGitChanges( canvas );

		_ = UpdateSyncStatusAsync();

		canvas.Layout.AddStretchCell();
	}

	private void OnCommitClicked()
	{
		if ( string.IsNullOrWhiteSpace( _commitMessage.PlainText ) )
		{
			Log.Warning( "Please enter a commit message!" );
			return;
		}

		CommitGitChanges();
	}

	// --- Autorefresh lifecycle ---

	private void SetupAutoRefresh()
	{
		_watcher = new FileSystemWatcher( Project.Current.GetRootPath() );
		_watcher.IncludeSubdirectories = true;
		_watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Size | NotifyFilters.CreationTime;

		_watcher.Changed += OnFileChanged;
		_watcher.Created += OnFileChanged;
		_watcher.Deleted += OnFileChanged;
		_watcher.Renamed += OnFileChanged;

		_watcher.EnableRaisingEvents = true;
	}

	private void OnFileChanged( object sender, FileSystemEventArgs e )
	{
		if ( IsInternalToolPath( e.FullPath ) )
		{
			return;
		}

		_needsRefresh = true;
	}

	private static bool IsInternalToolPath( string fullPath )
	{
		if ( string.IsNullOrWhiteSpace( fullPath ) )
		{
			return false;
		}

		var normalized = fullPath.Replace( '\\', '/' );
		return normalized.Contains( "/.git/", StringComparison.OrdinalIgnoreCase )
			|| normalized.EndsWith( "/.git", StringComparison.OrdinalIgnoreCase );
	}

	[EditorEvent.Frame]
	public void FrameUpdate()
	{
		if ( _needsRefresh && _lastRefresh > 0.5f )
		{
			_needsRefresh = false;
			_lastRefresh = 0;

			if ( _changesView.Visible )
			{
				OnRefreshClicked();
			}
		}
	}

	public override void OnDestroyed()
	{
		if ( _watcher != null )
		{
			_watcher.EnableRaisingEvents = false;
			_watcher.Changed -= OnFileChanged;
			_watcher.Created -= OnFileChanged;
			_watcher.Deleted -= OnFileChanged;
			_watcher.Renamed -= OnFileChanged;
			_watcher.Dispose();
			_watcher = null;
		}

		base.OnDestroyed();
	}
}
