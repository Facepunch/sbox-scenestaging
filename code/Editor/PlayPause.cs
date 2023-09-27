using Sandbox;

namespace Editor.Internal;

internal class PlayPauseWidget : Widget
{
	[Event( "tools.headerbar.build" )]
	public static void OnBuildHeaderToolbar( HeadBarEvent e )
	{
		new PlayPauseWidget( e.Center.AddRow() );
		new PlayPauseWidget( e.Center.AddRow() );
	}

	public PlayPauseWidget( Layout layout ) : base( null )
	{
		MinimumHeight = Theme.RowHeight;

		layout.Add( new PlayButton() );
		layout.Add( new PauseButton() );
	}
}

file class PlayButton : Widget
{
	public PlayButton() : base( null )
	{
		ToolTip = "Play/Stop";
		StatusTip = "Play/Stop";

		FixedWidth = Theme.RowHeight;
		FixedHeight = Theme.RowHeight;

		Cursor = CursorShape.Finger;
	}

	protected override void OnMousePress( MouseEvent e )
	{
		if ( !GameManager.IsPlaying )
		{
			EditorScene.Play();
		}
		else
		{
			EditorScene.Stop();
		}
	}

	protected override void OnDoubleClick( MouseEvent e )
	{
		// ignore
	}

	protected override void OnPaint()
	{
		Paint.Antialiasing = true;
		var paintColor = Color.White;

		var fontSize = 20;
		var icon = "play_arrow";
		bool active = GameManager.IsPlaying;

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
			Paint.SetBrush( Theme.ControlBackground.Darken( 0.5f ) );
		}

		Paint.DrawRect( LocalRect, 3 );

		if ( !Paint.HasMouseOver )
		{
			paintColor = paintColor.WithAlphaMultiplied( 0.75f );
			fontSize = 18;
		}

		Paint.SetPen( paintColor );
		Paint.DrawIcon( LocalRect, icon, fontSize, TextFlag.Center );
	}

}

file class PauseButton : Widget
{
	public PauseButton() : base( null )
	{
		ToolTip = "Pause";
		StatusTip = "Pause";

		FixedWidth = Theme.RowHeight;
		FixedHeight = Theme.RowHeight;

		Cursor = CursorShape.Finger;
	}

	protected override void OnMousePress( MouseEvent e )
	{
		GameManager.IsPaused = !GameManager.IsPaused;
	}

	protected override void OnDoubleClick( MouseEvent e )
	{
		// ignore
	}

	protected override void OnPaint()
	{
		Paint.Antialiasing = true;
		var paintColor = Color.White;

		var fontSize = 20;
		var icon = "pause";
		bool active = GameManager.IsPaused;

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
			Paint.SetBrush( Theme.ControlBackground.Darken( 0.5f ) );
		}

		Paint.DrawRect( LocalRect, 3 );

		if ( !Paint.HasMouseOver )
		{
			paintColor = paintColor.WithAlphaMultiplied( 0.75f );
			fontSize = 18;
		}

		Paint.SetPen( paintColor );
		Paint.DrawIcon( LocalRect, icon, fontSize, TextFlag.Center );
	}

}
