using System;
using Facepunch.ActionJigs;

namespace Editor.ActionJigs;

public class ErrorList : Widget
{
	public Button WarningsButton;
	public Button ErrorsButton;
	public Button InfoButton;
	public ErrorListView ErrorListView;

	bool ShowErrors = true;
	bool ShowWarnings = true;
	bool ShowInfo = true;

	internal static Color WarningColor => Theme.Yellow;
	internal static Color ErrorColor => Theme.Red;
	internal static Color InfoColor => Theme.Blue;

	public List<ValidationMessage> Messages { get; } = new ();
	public MainWindow Editor { get; }

	private int _lastMessagesHash;

	public ErrorList( Widget parent, MainWindow editor ) : base( parent )
	{
		Editor = editor;

		Name = "ErrorList";
		MinimumSize = new( 100, 100 );

		Layout = Layout.Column();

		var layout = Layout.Add( Layout.Row() );
		layout.Spacing = 8;
		layout.Margin = 5;

		ErrorsButton = new Button( "0 Errors", "error", this )
		{
			Clicked = () => { ShowErrors = !ShowErrors; UpdateErrors(); ErrorsButton.Update(); },
			OnPaintOverride = () => PaintShittyButton( ErrorsButton, "error", ErrorColor, ShowErrors ),
			StatusTip = "Toggle display of errors",
		};

		WarningsButton = new Button( "0 Warnings", "warning", this )
		{
			Clicked = () => { ShowWarnings = !ShowWarnings; UpdateErrors(); WarningsButton.Update(); },
			OnPaintOverride = () => PaintShittyButton( WarningsButton, "warning", WarningColor, ShowWarnings ),
			StatusTip = "Toggle display of warnings",
		};


		InfoButton = new Button( "0 Messages", "info", this )
		{
			Clicked = () => { ShowInfo = !ShowInfo; UpdateErrors(); InfoButton.Update(); },
			OnPaintOverride = () => PaintShittyButton( InfoButton, "info", InfoColor, ShowInfo ),
			StatusTip = "Toggle display of information",
		};

		layout.Add( ErrorsButton );
		layout.Add( WarningsButton );
		layout.Add( InfoButton );

		layout.AddStretchCell();

		var clearButton = new Button( "", "delete", this )
		{
			ButtonType = "clear",
			Clicked = () => { Messages.Clear(); UpdateErrors(); },
			StatusTip = "Clear error list"
		};
		clearButton.SetProperty( "cssClass", "clear" );
		layout.Add( clearButton );

		ErrorListView = new ErrorListView( this );
		Layout.Add( ErrorListView, 1 );

		UpdateErrors();
	}

	[EditorEvent.Frame]
	private void Frame()
	{
		UpdateErrors();
	}

	public void UpdateErrors()
	{
		Messages.Clear();
		Messages.AddRange( Editor.ActionJig.Messages );

		var hash = 0;

		foreach ( var message in Messages )
		{
			hash = HashCode.Combine( hash, message.GetHashCode() );
		}

		if ( _lastMessagesHash == hash )
		{
			return;
		}

		_lastMessagesHash = hash;

		var q = Messages.AsEnumerable();

		WarningsButton.Text = $"{q.Count( x => x.Level == MessageLevel.Warning )} Warnings";
		ErrorsButton.Text = $"{q.Count( x => x.Level == MessageLevel.Error )} Errors";
		InfoButton.Text = $"{q.Count( x => x.Level == MessageLevel.Info )} Messages";

		if ( !ShowErrors ) q = q.Where( x => x.Level != MessageLevel.Error );
		if ( !ShowWarnings ) q = q.Where( x => x.Level != MessageLevel.Warning );
		if ( !ShowInfo ) q = q.Where( x => x.Level != MessageLevel.Info );

		q = q.OrderByDescending( x => x.Level == MessageLevel.Error );

		ErrorListView.SetItems( q.Cast<object>() );
	}

	private bool PaintShittyButton( Button btn, string icon, Color color, bool active )
	{
		var rect = btn.LocalRect;

		Paint.SetBrush( Theme.Primary.WithAlpha( Paint.HasMouseOver ? 0.2f : 0.1f ) );
		Paint.ClearPen();

		if ( active )
		{
			Paint.SetPen( Theme.Primary.WithAlpha( 0.4f ), 2.0f );
			Paint.DrawRect( rect, 2 );
		}

		rect = rect.Shrink( 8, 3 );

		Paint.Antialiasing = true;
		Paint.SetPen( color.WithAlpha( Paint.HasMouseOver ? 1 : 0.7f ), 3.0f );
		Paint.ClearBrush();

		// Severity Icon
		var iconRect = rect;
		iconRect.Left += 0;
		iconRect.Width = 16;
		Paint.DrawIcon( iconRect, icon, 16 );

		rect.Left = iconRect.Right + 2;
		Paint.SetDefaultFont();
		Paint.SetPen( Theme.White.WithAlpha( active ? 1 : 0.4f ), 3.0f );
		Paint.DrawText( rect, btn.Text, TextFlag.Center );

		return true;
	}
}

public class ErrorListView : ListView
{
	public new ErrorList Parent => base.Parent as ErrorList;

	public ErrorListView( ErrorList parent ) : base( parent )
	{
		Name = "Output";

		ItemActivated = ( a ) =>
		{
			if ( a is ValidationMessage message )
			{
				SelectContext( message.Context );
			}
		};

		ItemContextMenu = OpenItemContextMenu;
		ItemSize = new Vector2( 0, 48 );
		ItemSpacing = 0;
		Margin = 0;
	}

	private void SelectContext( IMessageContext context )
	{
		switch ( context )
		{
			case Node node:
				Parent.Editor.View.SelectNode( node );
				return;

			case Node.Property property:
				Parent.Editor.View.SelectNode( property.Node );
				break;

			case Node.Input input:
				if ( input.IsLinked )
				{
					Parent.Editor.View.SelectLinks( input.Links );
				}
				else
				{
					Parent.Editor.View.SelectNode( input.Node );
				}
				break;

			case Node.Output output:
				Parent.Editor.View.SelectNode( output.Node );
				break;

			case Link link:
				Parent.Editor.View.SelectLink( link );
				break;
		}

		Parent.Editor.View.CenterOnSelection();
	}

	private void OpenItemContextMenu( object item )
	{
		if ( item is not ValidationMessage message )
			return;

		var m = new Menu();

		m.AddOption( "Show Context", "file_open",
			() => SelectContext( message.Context ) );
		m.AddOption( "Copy Error", "content_copy", () => EditorUtility.Clipboard.Copy( message.Value ) );

		m.OpenAt( Application.CursorPosition );
	}

	private static string FormatContext( IMessageContext context )
	{
		if ( context is Node node )
		{
			return node.GetDisplayInfo().Name;
		}

		if ( context is Node.Property property )
		{
			return $"{property.Display.Title ?? property.Name} - {FormatContext( property.Node )}";
		}

		if ( context is Node.Input input )
		{
			return $"{input.Display.Title ?? input.Name} - {FormatContext(input.Node)}";
		}

		if ( context is Node.Output output )
		{
			return $"{output.Display.Title ?? output.Name} - {FormatContext( output.Node )}";
		}

		if ( context is Link link )
		{
			return FormatContext( link.Target );
		}

		if ( context is IActionJig actionJig )
		{
			return actionJig.GetName();
		}

		if ( context is Variable variable )
		{
			return variable.Name;
		}

		return context.ToString();
	}

	protected override void PaintItem( VirtualWidget item )
	{
		if ( item.Object is not ValidationMessage message )
			return;

		(Color color, string icon) = message.Level switch
		{
			MessageLevel.Error => (ErrorList.ErrorColor, "error"),
			MessageLevel.Warning => (ErrorList.WarningColor, "warning"),
			_ => (ErrorList.InfoColor, "info"),
		};

		Paint.SetBrush( color.WithAlpha( Paint.HasMouseOver ? 0.1f : 0.03f ) );
		Paint.ClearPen();
		Paint.DrawRect( item.Rect.Shrink( 0, 1 ) );

		Paint.Antialiasing = true;
		Paint.SetPen( color.WithAlpha( Paint.HasMouseOver ? 1 : 0.7f ), 3.0f );
		Paint.ClearBrush();

		// Severity Icon
		var iconRect = item.Rect.Shrink( 12, 0 );
		iconRect.Width = 24;
		Paint.DrawIcon( iconRect, icon, 24 );

		var rect = item.Rect.Shrink( 48, 8, 0, 8 );

		Paint.SetPen( Theme.White.WithAlpha( Paint.HasMouseOver ? 1 : 0.8f ), 3.0f );
		Paint.DrawText( rect, message.Value, TextFlag.LeftTop | TextFlag.SingleLine );

		Paint.SetPen( Theme.White.WithAlpha( Paint.HasMouseOver ? 0.5f : 0.4f ), 3.0f );
		Paint.DrawText( rect, FormatContext( message.Context ),
			TextFlag.LeftBottom | TextFlag.SingleLine );
	}
}
