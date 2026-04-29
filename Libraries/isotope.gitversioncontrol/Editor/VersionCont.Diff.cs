using Editor;
using Sandbox;
using Sandbox.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using Checkbox = Editor.Checkbox;
using Label = Editor.Label;

public partial class LocalControlWidget
{
	// --- Diff Window Implementation ---

	public class DiffWindow : Window
	{
		private string _oldContent;
		private string _newContent;
		private string _fileName;

		private Widget _contentArea;
		private ScrollArea _leftScroll;
		private ScrollArea _rightScroll;
		private Checkbox _syncCb;

		private bool _syncScrolling = true;
		private bool _lineLeveling = true;
		private bool _wordWrap = false;

		private float _lastVerticalScroll = -1;

		public DiffWindow( string fileName, string oldContent, string newContent )
		{
			_fileName = fileName;
			_oldContent = oldContent;
			_newContent = newContent;

			WindowTitle = $"Diff: {fileName}";
			Size = new Vector2( 1300, 850 );

			var root = new Widget( null );
			root.Layout = Layout.Column();
			root.Layout.Margin = 10;
			root.Layout.Spacing = 0;

			BuildToolbar( root );

			_contentArea = new Widget( root );
			_contentArea.Layout = Layout.Column();
			root.Layout.Add( _contentArea, 1 );

			RefreshDiffView();
			Canvas = root;
		}

		private void BuildToolbar( Widget parent )
		{
			var toolbar = parent.Layout.AddRow();
			toolbar.Spacing = 20;
			toolbar.Margin = new Margin( 0, 0, 0, 12 );

			var syncGroup = toolbar.AddRow();
			syncGroup.Spacing = 5;
			_syncCb = new Checkbox( parent );
			_syncCb.Value = _syncScrolling;
			_syncCb.Toggled += () => { _syncScrolling = _syncCb.Value; };
			syncGroup.Add( _syncCb );
			syncGroup.Add( new Label( "Sync Scroll", parent ) );

			var levelGroup = toolbar.AddRow();
			levelGroup.Spacing = 5;
			var levelCb = new Checkbox( parent );
			levelCb.Value = _lineLeveling;
			levelCb.Toggled += () => { _lineLeveling = levelCb.Value; RefreshDiffView(); };
			levelGroup.Add( levelCb );
			levelGroup.Add( new Label( "Align Lines", parent ) );

			var wrapGroup = toolbar.AddRow();
			wrapGroup.Spacing = 5;
			var wrapCb = new Checkbox( parent );
			wrapCb.Value = _wordWrap;
			wrapCb.Toggled += () => { _wordWrap = wrapCb.Value; RefreshDiffView(); };
			wrapGroup.Add( wrapCb );
			wrapGroup.Add( new Label( "Word Wrap", parent ) );

			toolbar.AddStretchCell();
		}

		private void RefreshDiffView()
		{
			_contentArea.DestroyChildren();
			_lastVerticalScroll = -1;

			var header = _contentArea.Layout.AddRow();
			header.Margin = new Margin( 0, 0, 0, 4 );
			header.Add( new Label( "<b><font color='#ff7777'>  - PREVIOUS SNAPSHOT</font></b>" ), 1 );
			header.Add( new Label( "<b><font color='#77ff77'>  + CURRENT VERSION</font></b>" ), 1 );

			var compareRow = _contentArea.Layout.AddRow();
			compareRow.Spacing = 10;

			var (oldHtml, newHtml) = GenerateSplitDiffHtml( _oldContent, _newContent );

			_leftScroll = CreateDiffSide( _contentArea, oldHtml );
			_rightScroll = CreateDiffSide( _contentArea, newHtml );

			compareRow.Add( _leftScroll, 1 );
			compareRow.Add( _rightScroll, 1 );
		}

		private ScrollArea CreateDiffSide( Widget parent, string html )
		{
			var scroll = new ScrollArea( parent );
			var container = new Widget( scroll );
			container.Layout = Layout.Column();
			var label = new Label( html, container );
			label.WordWrap = _wordWrap;
			container.Layout.Add( label );
			container.Layout.AddStretchCell();
			scroll.Canvas = container;
			return scroll;
		}

		[EditorEvent.Frame]
		public void OnFrame()
		{
			if ( !_syncScrolling || _leftScroll == null || _rightScroll == null ) return;

			int leftV = _leftScroll.VerticalScrollbar.Value;
			int rightV = _rightScroll.VerticalScrollbar.Value;

			if ( leftV != _lastVerticalScroll )
			{
				_rightScroll.VerticalScrollbar.Value = leftV;
				_lastVerticalScroll = leftV;
			}
			else if ( rightV != _lastVerticalScroll )
			{
				_leftScroll.VerticalScrollbar.Value = rightV;
				_lastVerticalScroll = rightV;
			}
		}

		private (string oldHtml, string newHtml) GenerateSplitDiffHtml( string oldText, string newText )
		{
			var oldLines = oldText.Split( new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None );
			var newLines = newText.Split( new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None );
			var diffs = ComputeLineDiff( oldLines, newLines );

			string wrapStyle = _wordWrap ? "white-space: pre-wrap; word-break: break-all;" : "white-space: pre;";
			string tableBase = $"<table border='0' cellspacing='0' cellpadding='0' style='font-family: Consolas, monospace; font-size: 12px; width: 100%; {wrapStyle} line-height: 1.4;'>";

			string oldRes = tableBase; string newRes = tableBase;
			int oPtr = 1; int nPtr = 1;

			string gutter = "background-color: #1d1d1d; color: #555; width: 40px; text-align: right; padding-right: 8px; border-right: 1px solid #333;";
			string content = "padding-left: 8px;";

			string spacerRow = $"<tr><td style='{gutter}'>&nbsp;</td><td style='background-color: #1a1a1a; {content}'>&nbsp;</td></tr>";

			for ( int i = 0; i < diffs.Count; i++ )
			{
				var d = diffs[i];

				if ( _lineLeveling && d.Type == DiffType.Delete && i + 1 < diffs.Count && diffs[i + 1].Type == DiffType.Add )
				{
					var next = diffs[i + 1];
					var (hOld, hNew) = DiffCharacters( d.Text, next.Text );
					oldRes += $"<tr style='background-color: #321e1e;'><td style='{gutter}'>{oPtr++}</td><td style='{content}'>{hOld}</td></tr>";
					newRes += $"<tr style='background-color: #12261e;'><td style='{gutter}'>{nPtr++}</td><td style='{content}'>{hNew}</td></tr>";
					i++;
					continue;
				}

				string esc = System.Security.SecurityElement.Escape( d.Text );
				if ( d.Type == DiffType.Match )
				{
					oldRes += $"<tr><td style='{gutter}'>{oPtr++}</td><td style='{content}'>{esc}</td></tr>";
					newRes += $"<tr><td style='{gutter}'>{nPtr++}</td><td style='{content}'>{esc}</td></tr>";
				}
				else if ( d.Type == DiffType.Add )
				{
					if ( _lineLeveling ) oldRes += spacerRow;
					newRes += $"<tr style='background-color: #12261e;'><td style='{gutter}'>{nPtr++}</td><td style='{content}'>{esc}</td></tr>";
				}
				else if ( d.Type == DiffType.Delete )
				{
					oldRes += $"<tr style='background-color: #321e1e;'><td style='{gutter}'>{oPtr++}</td><td style='{content}'>{esc}</td></tr>";
					if ( _lineLeveling ) newRes += spacerRow;
				}
			}
			return (oldRes + "</table>", newRes + "</table>");
		}

		private enum DiffType { Match, Add, Delete }
		private struct LineDiff { public DiffType Type; public string Text; }

		private List<LineDiff> ComputeLineDiff( string[] oldLines, string[] newLines )
		{
			int n = oldLines.Length, m = newLines.Length;
			int[,] lp = new int[n + 1, m + 1];
			for ( int i = 1; i <= n; i++ )
				for ( int j = 1; j <= m; j++ )
					lp[i, j] = (oldLines[i - 1].Trim() == newLines[j - 1].Trim()) ? lp[i - 1, j - 1] + 1 : Math.Max( lp[i - 1, j], lp[i, j - 1] );

			var res = new List<LineDiff>();
			int x = n, y = m;
			while ( x > 0 || y > 0 )
			{
				if ( x > 0 && y > 0 && oldLines[x - 1].Trim() == newLines[y - 1].Trim() )
				{
					res.Add( new LineDiff { Type = DiffType.Match, Text = oldLines[x - 1] } );
					x--; y--;
				}
				else if ( y > 0 && (x == 0 || lp[x, y - 1] >= lp[x - 1, y]) )
				{
					res.Add( new LineDiff { Type = DiffType.Add, Text = newLines[y - 1] } );
					y--;
				}
				else
				{
					res.Add( new LineDiff { Type = DiffType.Delete, Text = oldLines[x - 1] } );
					x--;
				}
			}
			res.Reverse();
			return res;
		}

		private (string oldOut, string newOut) DiffCharacters( string oldLine, string newLine )
		{
			int start = 0;
			while ( start < oldLine.Length && start < newLine.Length && oldLine[start] == newLine[start] ) start++;
			int oEnd = oldLine.Length - 1, nEnd = newLine.Length - 1;
			while ( oEnd >= start && nEnd >= start && oldLine[oEnd] == newLine[nEnd] ) { oEnd--; nEnd--; }
			string sOld = System.Security.SecurityElement.Escape( oldLine.Substring( 0, start ) );
			string mOld = $"<span style='background-color: #792e2d; color: white;'>{System.Security.SecurityElement.Escape( oldLine.Substring( start, Math.Max( 0, oEnd - start + 1 ) ) )}</span>";
			string eOld = System.Security.SecurityElement.Escape( oldLine.Substring( oEnd + 1 ) );
			string sNew = System.Security.SecurityElement.Escape( newLine.Substring( 0, start ) );
			string mNew = $"<span style='background-color: #1d572d; color: white;'>{System.Security.SecurityElement.Escape( newLine.Substring( start, Math.Max( 0, nEnd - start + 1 ) ) )}</span>";
			string eNew = System.Security.SecurityElement.Escape( newLine.Substring( nEnd + 1 ) );
			return (sOld + mOld + eOld, sNew + mNew + eNew);
		}
	}

	public class ClickableLabel : Label
	{
		public Action DoubleClicked { get; set; }
		public Action<Vector2> RightClicked { get; set; }
		public ClickableLabel( string text, Widget parent ) : base( text, parent ) { }
		protected override void OnDoubleClick( MouseEvent e ) { base.OnDoubleClick( e ); DoubleClicked?.Invoke(); }
		protected override void OnContextMenu( ContextMenuEvent e ) { base.OnContextMenu( e ); RightClicked?.Invoke( e.ScreenPosition ); e.Accepted = true; }
	}

	private void ShowOperationNotification( string title, string subtitle, string icon, Color borderColor, float seconds = 2.5f )
	{
		var toast = new OperationToast { Title = title, Subtitle = subtitle, Icon = icon, BorderColor = borderColor, DrawTimer = false, IsRunning = false, FixedWidth = 320, FixedHeight = 72 };
		ToastManager.Remove( toast, seconds );
	}

	private class OperationToast : ToastWidget
	{
		public override bool WantsVisible => EditorPreferences.NotificationPopups;
		protected override void OnPaint() { if ( !EditorPreferences.NotificationPopups ) return; base.OnPaint(); }
	}
}
