using System;
using System.Diagnostics;
using System.Net;

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

		layout.Spacing = 5;
		layout.Margin = new Sandbox.UI.Margin( 16, 0 );
		layout.Add( new NetworkStatus() );
	}
}

file class NetworkStatus : Widget
{
	public static Color DeactivatedColor => Theme.WidgetBackground.Darken( 0.2f );

	public NetworkStatus() : base( null )
	{
		ToolTip = "Network";
		StatusTip = "Network";

		FixedWidth = Theme.RowHeight;
		FixedHeight = Theme.RowHeight;

		Cursor = CursorShape.Finger;
		MouseTracking = true;
	}

	protected override void OnMousePress( MouseEvent e )
	{
		var menu = new Menu( this );

		menu.AddOption( new Option( "Start Hosting", "dns", () => EditorUtility.Network.StartHosting() ) { Enabled = !EditorUtility.Network.Active } );
		menu.AddOption( new Option( "Disconnect", "phonelink_erase", () => EditorUtility.Network.Disconnect() ) { Enabled = EditorUtility.Network.Active } );

		menu.AddSeparator();

		menu.AddOption( new Option( "Join via new instance", "connected_tv", () => SpawnProcess() ) { Enabled = EditorUtility.Network.Hosting } );

		menu.OpenAtCursor();
		// menu
	}

	void SpawnProcess()
	{
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

	[EditorEvent.Frame]
	public void CheckForChanges()
	{
		SetContentHash( HashCode.Combine( EditorUtility.Network.Active ), 0.1f );
	}

	protected override void OnPaint()
	{
		Paint.Antialiasing = true;
		var paintColor = Theme.Red.Darken( 0.5f ).Desaturate( 0.2f );

		if ( EditorUtility.Network.Active )
		{
			paintColor = Theme.Green.Darken( 0.4f );
		}

		Paint.ClearPen();
		Paint.SetBrush( paintColor );
		Paint.DrawRect( LocalRect.Shrink( 1 ), 30 );

		Paint.SetPen( paintColor.Lighten( 0.5f ) );

		if ( EditorUtility.Network.Hosting )
		{
			Paint.DrawIcon( LocalRect.Shrink( 2 ), "dns", 11 );
		}
		else
		{
			Paint.DrawIcon( LocalRect.Shrink( 2 ), "wifi", 11 );
		}

		
	}

}
