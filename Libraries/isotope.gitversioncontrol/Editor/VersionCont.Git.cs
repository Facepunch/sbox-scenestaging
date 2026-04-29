using Editor;
using Sandbox;
using Sandbox.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Button = Editor.Button;
using Label = Editor.Label;

public partial class LocalControlWidget
{

	private class GitCommitView
	{
		public string Hash;
		public string Date;
		public string Message;
		public List<string> Files;
	}

	// --- History UI ---

	private void RefreshGitHistoryUI( Widget canvas )
	{
		if ( !EnsureGitRepository() )
		{
			canvas.Layout.Add( new Label( "No Git repository found." ) );
			canvas.Layout.AddStretchCell();
			return;
		}

		var unpushedSet = new HashSet<string>();
		var (syncOk, syncOut, _) = RunGitCommand( "rev-list HEAD --not --remotes" );
		if ( syncOk && !string.IsNullOrWhiteSpace( syncOut ) )
		{
			foreach ( var h in syncOut.Split( new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries ) )
			{
				unpushedSet.Add( h.Trim().ToLower() );
			}
		}

		var (ok, output, error) = RunGitCommand( "log --date=format:%Y-%m-%d_%H:%M --pretty=format:%H|%ad|%s --name-only --max-count=30" );
		if ( !ok )
		{
			canvas.Layout.Add( new Label( $"Git log failed: {error}" ) );
			canvas.Layout.AddStretchCell();
			return;
		}

		var lines = output.Split( new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None );
		var commits = new List<GitCommitView>();
		GitCommitView current = null;

		foreach ( var raw in lines )
		{
			var line = raw?.TrimEnd() ?? "";
			if ( string.IsNullOrWhiteSpace( line ) ) continue;

			if ( line.Count( c => c == '|' ) >= 2 )
			{
				var parts = line.Split( '|', 3 );
				string dateString = parts[1];
				var dateParts = dateString.Split( '_' );
				string formattedTimestamp = dateString.Replace( "_", " " );

				if ( dateParts.Length == 2 )
					formattedTimestamp = $"{dateParts[0]} <u>{dateParts[1]}</u>";

				current = new GitCommitView
				{
					Hash = parts[0].Trim().ToLower(),
					Date = formattedTimestamp,
					Message = parts[2],
					Files = new List<string>()
				};
				commits.Add( current );
			}
			else if ( current != null )
			{
				current.Files.Add( line.Trim() );
			}
		}
		foreach ( var commit in commits )
		{
			var card = new Widget( canvas );
			card.Layout = Layout.Column();
			card.Layout.Margin = new Margin( 10, 8 );
			card.Layout.Spacing = 4;

			card.Layout.Add( new Label( $"<b>💬 {commit.Message}</b>", card ) { WordWrap = true } );

			var actionRow = card.Layout.AddRow();
			actionRow.Spacing = 5;

			actionRow.Add( new Label( $"<small><font color='#ffffff'>{commit.Date}</font></small>", card ) );

			string shortHash = commit.Hash.Length > 7 ? commit.Hash.Substring( 0, 7 ) : commit.Hash;
			string webUrl = GetCommitWebUrl( commit.Hash );

			bool isPushed = !unpushedSet.Contains( commit.Hash ) && !string.IsNullOrEmpty( webUrl );

			var hashLabel = new ClickableLabel( string.Empty, card );

			if ( isPushed )
			{
				hashLabel.Text = $"<small><font color='#55aaff'><b><u>• {shortHash}</u></b></font></small>";
				hashLabel.ToolTip = $"Double click to open commit {shortHash} on Web";
				hashLabel.DoubleClicked = () => Process.Start( new ProcessStartInfo( webUrl ) { UseShellExecute = true } );
			}
			else
			{
				hashLabel.Text = $"<small><font color='#bbbbbb'><b><u>• {shortHash}</u></b></font></small>";
				hashLabel.ToolTip = string.IsNullOrEmpty( webUrl ) ? "Set remote to enable links" : "Commit local only (Not Pushed)";
			}

			actionRow.Add( hashLabel );
			actionRow.AddStretchCell();

			var viewBtn = new Button( "View", "visibility" );
			actionRow.Add( viewBtn );

			var filesContainer = new Widget( card ) { Visible = false };
			filesContainer.Layout = Layout.Column();
			filesContainer.Layout.Margin = new Margin( 20, 5, 0, 5 );
			filesContainer.Layout.Spacing = 8;

			viewBtn.Clicked += () => {
				filesContainer.Visible = !filesContainer.Visible;
				viewBtn.Text = filesContainer.Visible ? "Hide" : "View";
				viewBtn.Icon = filesContainer.Visible ? "visibility_off" : "visibility";
			};

			var capturedHash = commit.Hash;
			var capturedMsg = commit.Message;
			var restoreBtn = new Button( "Restore to this state", "settings_backup_restore" );
			restoreBtn.ToolTip = "Restores all files to their state in this commit. This does not move your branch.\nRestored files will appear in Changes for you to review and commit.";
			restoreBtn.Clicked += () => OnGitRestoreClicked( capturedHash, capturedMsg );
			filesContainer.Layout.AddRow().Add( restoreBtn, 1 );

			filesContainer.Layout.AddSeparator();
			filesContainer.Layout.Add( new Label( $"<small><b>{commit.Files.Count} file(s) changed:</b></small>" ) );
			foreach ( var f in commit.Files ) filesContainer.Layout.Add( new Label( $"<small>  📄 {f}</small>" ) );

			card.Layout.Add( filesContainer );
			canvas.Layout.Add( card );
			canvas.Layout.AddSeparator();
		}
		canvas.Layout.AddStretchCell();
	}

	// --- Restore ---

	private void OnGitRestoreClicked( string hash, string commitMessage )
	{
		if ( !EnsureGitRepository() )
		{
			return;
		}

		var (statusOk, statusOut, _) = RunGitCommand( "status --porcelain" );
		bool hasUncommitted = statusOk && !string.IsNullOrWhiteSpace( statusOut );

		string prompt = hasUncommitted
			? $"You have uncommitted changes that will be overwritten.\n\nRestore files to commit '{commitMessage}' ({hash})?\n\nThis does not move your branch — restored files will appear in Changes for you to review and commit."
			: $"Restore files to commit '{commitMessage}' ({hash})?\n\nThis does not move your branch — restored files will appear in Changes for you to review and commit.";

		Editor.Dialog.AskConfirm(
			() => PerformGitRestore( hash ),
			prompt,
			"Restore Git Commit?"
		);
	}

	private void PerformGitRestore( string hash )
	{
		var (restoreOk, _, restoreError) = RunGitCommand( $"restore --source={QuoteArg( hash )} --worktree -- ." );
		if ( !restoreOk )
		{
			var (checkoutOk, _, checkoutError) = RunGitCommand( $"checkout {QuoteArg( hash )} -- ." );
			if ( !checkoutOk )
			{
				var finalError = string.IsNullOrWhiteSpace( restoreError ) ? checkoutError : restoreError;
				Log.Error( $"Git restore failed: {finalError}" );
				ShowOperationNotification( "Restore Failed", string.IsNullOrWhiteSpace( finalError ) ? "Unable to restore commit." : finalError, "error", Theme.Red );
				return;
			}
		}

		RunGitCommand( "reset -- ." );

		ShowOperationNotification( "Restore Applied", "Files restored — review and commit when ready", "settings_backup_restore", Theme.Green );
		ShowTab( true );
	}

	// --- Changes ---

	private void RefreshGitChanges( Widget canvas )
	{
		if ( !EnsureGitRepository() )
		{
			var initRow = canvas.Layout.AddRow();
			initRow.Add( new Label( "No Git repository found in this project." ) );

			var initBtn = new Button( "Init Git Now", "create_new_folder" );
			initBtn.Clicked += () =>
			{
				OnInitGitClicked();
				OnRefreshClicked();
			};

			canvas.Layout.AddSpacingCell( 8 );

			canvas.Layout.Add( initBtn );

			return;
		}

		var (ok, output, error) = RunGitCommand( "status --porcelain" );
		if ( !ok )
		{
			canvas.Layout.Add( new Label( $"Git status failed: {error}" ) );
			return;
		}

		var lines = output.Split( new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries );
		if ( lines.Length == 0 )
		{
			canvas.Layout.Add( new Label( "No changed files." ) );
			return;
		}

		foreach ( var line in lines )
		{
			if ( !TryParseGitStatusLine( line, out var code, out var path ) )
			{
				continue;
			}

			AddChangeItem( canvas, path, MapGitStatus( code ) );
		}
	}

	private bool TryParseGitStatusLine( string line, out string code, out string path )
	{
		code = "";
		path = "";

		if ( string.IsNullOrWhiteSpace( line ) || line.Length < 3 )
		{
			return false;
		}

		code = line.Substring( 0, 2 );
		path = line.Length > 3 ? line.Substring( 3 ).Trim() : string.Empty;

		if ( path.Contains( "->" ) )
		{
			path = path.Split( "->" ).Last().Trim();
		}

		if ( path.StartsWith( "\"" ) && path.EndsWith( "\"" ) && path.Length >= 2 )
		{
			path = path.Substring( 1, path.Length - 2 );
		}

		path = path.Replace( "\\", "/" );

		if ( string.IsNullOrWhiteSpace( path ) )
		{
			return false;
		}

		var root = Project.Current.GetRootPath();
		if ( !File.Exists( Path.Combine( root, path ) ) )
		{
			var fallback = line.Length > 2 ? line.Substring( 2 ).Trim() : path;
			if ( fallback.Contains( "->" ) )
			{
				fallback = fallback.Split( "->" ).Last().Trim();
			}

			if ( fallback.StartsWith( "\"" ) && fallback.EndsWith( "\"" ) && fallback.Length >= 2 )
			{
				fallback = fallback.Substring( 1, fallback.Length - 2 );
			}

			fallback = fallback.Replace( "\\", "/" );
			if ( File.Exists( Path.Combine( root, fallback ) ) )
			{
				path = fallback;
			}
		}

		return true;
	}

	private static string MapGitStatus( string code )
	{
		if ( code.Contains( "??" ) ) return "Untracked";
		if ( code.Contains( "A" ) ) return "Added";
		if ( code.Contains( "D" ) ) return "Deleted";
		if ( code.Contains( "R" ) ) return "Renamed";
		if ( code.Contains( "M" ) ) return "Modified";
		return "Changed";
	}

	// --- Commit ---

	private void CommitGitChanges()
	{
		if ( !EnsureGitRepository() )
		{
			Log.Warning( "No Git repository found. Click 'Init Git' first." );
			return;
		}

		var filesToCommit = _trackedRows.Where( x => x.Toggle.Value ).ToList();
		if ( filesToCommit.Count == 0 )
		{
			Log.Warning( "No files selected for commit." );
			return;
		}

		foreach ( var item in filesToCommit )
		{
			var (addOk, _, addErr) = RunGitCommand( $"add -- {QuoteArg( item.FilePath )}" );
			if ( !addOk )
			{
				Log.Error( $"Git add failed for '{item.FilePath}': {addErr}" );
				return;
			}
		}

		var (commitOk, commitOut, commitErr) = RunGitCommand( $"commit -m {QuoteArg( _commitMessage.PlainText )}" );
		if ( !commitOk )
		{
			Log.Warning( string.IsNullOrWhiteSpace( commitErr ) ? commitOut : commitErr );
			return;
		}

		Log.Info( "Git commit created successfully." );
		ShowOperationNotification( "Commit Created", $"{filesToCommit.Count} file(s) committed", "check_circle", Theme.Green );
		_commitMessage.PlainText = "";
		OnRefreshClicked();

		if ( _historyView.Visible )
		{
			RefreshHistoryUI();
		}
	}

	// --- Remote actions ---

	private void OnInitGitClicked()
	{
		if ( !IsGitAvailable() )
		{
			Log.Error( "Git is not installed or not found in PATH." );
			return;
		}

		if ( IsGitRepository() )
		{
			Log.Info( "Git repository already exists." );
			return;
		}

		var (ok, _, error) = RunGitCommand( "init -b main" );
		if ( !ok )
		{
			Log.Error( $"Failed to initialize Git repository: {error}" );
			return;
		}

		Log.Info( "Git repository initialized." );
		ShowOperationNotification( "Git Initialized", "Repository created for this project", "create_new_folder", Theme.Green );
		OnRefreshClicked();
	}

	private void OnConnectOriginClicked()
	{
		if ( !EnsureGitRepository() )
		{
			Log.Warning( "Initialize a Git repository first." );
			return;
		}

		var url = _gitRemoteUrl?.Value?.Trim();
		if ( string.IsNullOrWhiteSpace( url ) )
		{
			Log.Warning( "Enter a remote repository URL first." );
			return;
		}

		var (hasOrigin, _, _) = RunGitCommand( "remote get-url origin" );
		var args = hasOrigin
			? $"remote set-url origin {QuoteArg( url )}"
			: $"remote add origin {QuoteArg( url )}";

		var (ok, _, error) = RunGitCommand( args );
		if ( !ok )
		{
			Log.Error( $"Failed to configure remote: {error}" );
			return;
		}

		Log.Info( $"Remote origin set to: {url}" );
		ShowOperationNotification( "Remote Set", url, "hub", Theme.Green );
	}

	private void TryPopulateGitRemoteUrlFromOrigin()
	{
		if ( _gitRemoteUrl == null || !IsGitAvailable() || !IsGitRepository() )
		{
			return;
		}

		var (ok, output, _) = RunGitCommand( "remote get-url origin" );
		if ( ok && !string.IsNullOrWhiteSpace( output ) )
		{
			_gitRemoteUrl.Value = output.Trim();
		}
	}

	private void OnPullClicked()
	{
		if ( !EnsureGitRepository() ) return;

		Editor.Dialog.AskConfirm(
			() => _ = ExecutePullAsync(),
			"This will pull remote changes into your local project.\n\nContinue?",
			"Confirm Pull"
		);
	}

	private async Task ExecutePullAsync()
	{
		if ( _gitOperationInProgress )
		{
			Log.Warning( "A Git operation is already in progress." );
			return;
		}

		bool success = false;
		_gitOperationInProgress = true;
		try
		{
			var (ok, output, error) = await RunGitCommandAsync( "pull" );
			if ( !ok )
			{
				Log.Warning( string.IsNullOrWhiteSpace( error ) ? output : error );
				return;
			}

			Log.Info( "Pull completed." );
			ShowOperationNotification( "Pull Completed", "Remote changes applied", "download", Theme.Primary );
			success = true;
		}
		finally
		{
			_gitOperationInProgress = false;
		}

		if ( success )
		{
			OnRefreshClicked();

			if ( _historyView.Visible )
			{
				RefreshHistoryUI();
			}
		}
	}

	private async Task ExecutePushAsync()
	{
		if ( _gitOperationInProgress )
		{
			Log.Warning( "A Git operation is already in progress." );
			return;
		}

		bool success = false;
		_gitOperationInProgress = true;
		try
		{
			var (ok, output, error) = await RunGitCommandAsync( "push -u origin HEAD" );
			if ( !ok )
			{
				Log.Warning( string.IsNullOrWhiteSpace( error ) ? output : error );
				return;
			}

			Log.Info( "Push completed." );
			ShowOperationNotification( "Push Completed", "Commits uploaded to remote", "upload", Theme.Green );
			success = true;
		}
		finally
		{
			_gitOperationInProgress = false;
		}

		if ( success )
		{
			_ = UpdateSyncStatusAsync();

			if ( _historyView.Visible )
			{
				RefreshHistoryUI();
			}
		}
	}

	private void OnPushClicked()
	{
		if ( !EnsureGitRepository() ) return;

		Editor.Dialog.AskConfirm(
			() => _ = ExecutePushAsync(),
			"This will push your local commits to the remote repository.\n\nContinue?",
			"Confirm Push"
		);
	}

	// --- Git helpers ---

	private async Task UpdateSyncStatusAsync()
	{
		if ( !IsGitRepository() || _gitOperationInProgress ) return;

		var (ok, output, _) = await RunGitCommandAsync( "rev-list --left-right --count HEAD...@{u}" );

		if ( !ok || string.IsNullOrWhiteSpace( output ) )
		{
			_syncStatusLabel.Text = "<font color='#888'>Sync: Local only (no remote origin)</font>";
			return;
		}

		var parts = output.Split( '\t' );
		if ( parts.Length == 2 && int.TryParse( parts[0], out int ahead ) && int.TryParse( parts[1], out int behind ) )
		{
			if ( ahead == 0 && behind == 0 )
				_syncStatusLabel.Text = "<font color='#77ff77'> Up to date with remote</font>";
			else if ( ahead > 0 && behind == 0 )
				_syncStatusLabel.Text = $"<font color='#77ccff'> {ahead} commit(s) ahead (Ready to Push)</font>";
			else if ( ahead == 0 && behind > 0 )
				_syncStatusLabel.Text = $"<font color='#ffcc77'> {behind} commit(s) behind (Need to Pull)</font>";
			else
				_syncStatusLabel.Text = $"<font color='#ff7777'> {behind} behind /  {ahead} ahead (Diverged)</font>";
		}
	}

	private bool EnsureGitRepository()
	{
		return IsGitAvailable() && IsGitRepository();
	}

	private bool IsGitRepository()
	{
		return Directory.Exists( Path.Combine( Project.Current.GetRootPath(), ".git" ) );
	}

	private bool IsGitAvailable()
	{
		var (ok, _, _) = RunGitCommandRaw( "--version" );
		return ok;
	}

	private string ReadGitFileAtRevision( string revision, string relativePath )
	{
		relativePath = relativePath.Replace( "\\", "/" );
		var (ok, output, _) = RunGitCommand( $"show {revision}:{relativePath}" );
		return ok ? output : "[No previous version in Git history]";
	}

	private (bool ok, string output, string error) RunGitCommand( string arguments )
	{
		return RunGitCommandRaw( arguments );
	}

	private Task<(bool ok, string output, string error)> RunGitCommandAsync( string arguments )
	{
		return Task.Run( () => RunGitCommandRaw( arguments ) );
	}

	private (bool ok, string output, string error) RunGitCommandRaw( string arguments )
	{
		try
		{
			var startInfo = new ProcessStartInfo( "git", arguments )
			{
				WorkingDirectory = Project.Current.GetRootPath(),
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				CreateNoWindow = true
			};

			using var proc = Process.Start( startInfo );
			if ( proc == null )
			{
				return (false, "", "Failed to launch git process.");
			}

			string output = proc.StandardOutput.ReadToEnd();
			string error = proc.StandardError.ReadToEnd();
			proc.WaitForExit();
			return (proc.ExitCode == 0, output.Trim(), error.Trim());
		}
		catch ( Exception e )
		{
			return (false, "", e.Message);
		}
	}

	private static string QuoteArg( string input )
	{
		if ( input == null ) return "\"\"";
		return $"\"{input.Replace( "\\", "\\\\" ).Replace( "\"", "\\\"" )}\"";
	}

	private string GetCommitWebUrl( string hash )
	{
		var url = _gitRemoteUrl?.Value?.Trim();
		if ( string.IsNullOrWhiteSpace( url ) ) return null;

		if ( url.EndsWith( ".git" ) )
			url = url.Substring( 0, url.Length - 4 );

		return $"{url}/commit/{hash}";
	}
}
