using System.Diagnostics;

namespace Editor.HeaderBar;

internal class NetworkingBar : Widget
{
	[Event( "tools.headerbar.build", Priority = 100 )]
	public static void OnBuildHeaderToolbar( HeadBarEvent e )
	{
		new NetworkingBar( e.Center.AddRow() );
	}

	public NetworkingBar( Layout layout ) : base( null )
	{
		MinimumHeight = Theme.RowHeight;

		layout.Spacing = 1;
		layout.Margin = new Sandbox.UI.Margin( 16, 0 );
		layout.Add( new LaunchButton() );
	}
}

file class LaunchButton : Widget
{
	public static Color DeactivatedColor => Theme.WidgetBackground.Darken( 0.2f );

	public LaunchButton() : base( null )
	{
		ToolTip = "Join in external player";
		StatusTip = "Join in external player";

		FixedWidth = Theme.RowHeight;
		FixedHeight = Theme.RowHeight;

		Cursor = CursorShape.Finger;
		MouseTracking = true;
	}

	protected override void OnMousePress( MouseEvent e )
	{
		// if network isn't started, then start it
		ConsoleSystem.Run( "host" );

		var p = new Process();
		p.StartInfo.FileName = "sbox.exe";
		p.StartInfo.WorkingDirectory = System.Environment.CurrentDirectory;
		p.StartInfo.CreateNoWindow = true;
		p.StartInfo.RedirectStandardOutput = true;
		p.StartInfo.RedirectStandardError = true;
		p.StartInfo.UseShellExecute = false;

		p.StartInfo.ArgumentList.Add( "-sw" );
		p.StartInfo.ArgumentList.Add( "-joinlocal" );

		p.Start();
	}

	protected override void OnMouseMove( MouseEvent e )
	{
		base.OnMouseMove( e );
	}

	protected override void OnDoubleClick( MouseEvent e )
	{
		// ignore
	}

	protected override void OnPaint()
	{
		Paint.Antialiasing = true;
		var paintColor = Color.White;

		var rect = LocalRect.Shrink( 0, 0, 0, 0 );

		var fontSize = 15;
		var icon = "add_to_queue";
		bool active = false;

		if ( active )
		{
			paintColor = Color.White;

			Paint.ClearPen();
			Paint.SetBrush( Theme.Blue.Darken( 0.5f ) );

		}
		else
		{
			paintColor = Color.White.WithAlpha( 0.4f );
			Paint.ClearPen();
			Paint.SetBrush( DeactivatedColor );
		}

		Paint.DrawRect( rect, 3 );

		if ( !Paint.HasMouseOver )
		{
			paintColor = paintColor.WithAlphaMultiplied( 0.75f );
		}

		Paint.SetPen( paintColor );
		Paint.DrawIcon( LocalRect, icon, fontSize, TextFlag.Center );
	}

}
