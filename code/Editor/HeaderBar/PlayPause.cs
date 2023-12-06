using Sandbox;
using System.Reflection;

namespace Editor.Internal;

internal class PlayPauseWidget : Widget
{
	[Event( "tools.headerbar.build" )]
	public static void OnBuildHeaderToolbar( HeadBarEvent e )
	{
		new PlayPauseWidget( e.Center.AddRow() );
	}

	public PlayPauseWidget( Layout layout ) : base( null )
	{
		MinimumHeight = Theme.RowHeight;

		layout.Spacing = 1;
		layout.Add( new PlayButton() );
		layout.Add( new PauseButton() );
	}
}

file class PlayButton : Widget
{
	public static Color DeactivatedColor => Theme.WidgetBackground.Darken( 0.2f );

	const float dropdownWidth = 16;

	bool hoveringDropdown;

	public PlayButton() : base( null )
	{
		ToolTip = "Play/Stop";
		StatusTip = "Play/Stop";

		FixedWidth = 77 + dropdownWidth;
		FixedHeight = Theme.RowHeight;

		Cursor = CursorShape.Finger;
		MouseTracking = true;
	}

	protected override void OnMousePress( MouseEvent e )
	{
		if ( hoveringDropdown && !GameManager.IsPlaying )
		{
			var menu = new Menu( this );

			menu.AddOption( "Game", "sports_esports", () => EditorScene.PlayMode = "game" ).Enabled = EditorScene.PlayMode != "game";
			menu.AddOption( "Scene", "slideshow", () => EditorScene.PlayMode = "scene" ).Enabled = EditorScene.PlayMode != "scene";

			menu.MinimumWidth = Width;
			menu.OpenAt( ScreenRect.BottomLeft );
			return;
		}

		if ( !GameManager.IsPlaying )
		{
			EditorScene.Play();
		}
		else
		{
			EditorScene.Stop();
		}
	}


	protected override void OnMouseMove( MouseEvent e )
	{
		base.OnMouseMove( e );

		hoveringDropdown = e.LocalPosition.x > LocalRect.Width - dropdownWidth;

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
		var leftRect = new Rect( rect.Left, rect.Top, rect.Width - dropdownWidth, rect.Height );
		var rightRect = new Rect( rect.Right - dropdownWidth, rect.Top, dropdownWidth, rect.Height );

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
			Paint.SetBrush( DeactivatedColor );
		}

		Paint.DrawRect( rect, 3 );

		if ( !Paint.HasMouseOver )
		{
			paintColor = paintColor.WithAlphaMultiplied( 0.75f );
		}

		var txt = EditorScene.PlayMode;
		if ( txt == "scene" ) txt = "Scene";
		if ( txt == "game" ) txt = "Game";

		Paint.SetPen( paintColor );
		Paint.DrawIcon( leftRect.Shrink( 4, 0, 0, 0 ), icon, fontSize, TextFlag.LeftCenter );
		Paint.DrawText( leftRect.Shrink( 32, 0, 0, 0 ), txt, TextFlag.LeftCenter );


		if ( !GameManager.IsPlaying )
		{
			if ( Paint.HasMouseOver )
			{
				if ( hoveringDropdown )
				{

				}

				Paint.SetPen( Color.Black.WithAlpha( 0.4f ) );
				Paint.DrawLine( rightRect.TopLeft, rightRect.BottomLeft );
			}

			Paint.SetPen( Color.White.WithAlpha( (Paint.HasMouseOver && hoveringDropdown) ? 0.5f : (Paint.HasMouseOver ? 0.3f : 0.1f) ) );
			Paint.DrawIcon( rightRect, "arrow_drop_down", 14, TextFlag.Center );
		}
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
			Paint.SetBrush( PlayButton.DeactivatedColor );
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
