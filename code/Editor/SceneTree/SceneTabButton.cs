
using System;

public partial class SceneTabButton : Widget
{
	Scene scene;

	public SceneTabButton( Scene scene ) : base( null )
	{
		this.scene = scene;
		Cursor = CursorShape.Finger;
	}

	string Text
	{
		get
		{
			if ( IsLiveGame ) return "Current Game";

			var txt = scene.Name;
			if ( scene.HasUnsavedChanges ) return $"{txt}*";
			return txt;
		}
	}
	bool IsPrefab => scene.IsEditor && scene.SourcePrefabFile is not null;
	bool IsLiveGame => !scene.IsEditor;

	protected override Vector2 SizeHint()
	{
		var rect = Paint.MeasureText( LocalRect, Text, TextFlag.LeftCenter );
		var width = rect.Width + 8;

		return new Vector2( width, 22 );
	}

	protected override void OnMousePress( MouseEvent e )
	{
		if ( e.LeftMouseButton )
		{
			EditorScene.SwitchActive( scene );
		}

		if ( e.RightMouseButton )
		{
			OpenContextMenu();
		}
	}

	protected override void OnPaint()
	{
		Paint.Antialiasing = true;

		var pen = Color.White;
		if ( IsPrefab ) pen = Theme.Blue;
		if ( IsLiveGame ) pen = Theme.Green;

		bool active = EditorScene.Active == scene;

		if ( active )
		{
			Paint.ClearPen();
			Paint.SetBrush( pen.WithAlpha( 0.2f ) );
			Paint.DrawRect( LocalRect, 3 );
		}
		else if ( Paint.HasMouseOver )
		{
			Paint.ClearPen();
			Paint.SetBrush( pen.WithAlpha( 0.1f ) );
			Paint.DrawRect( LocalRect );
		}

		Paint.SetPen( pen.WithAlpha( 0.4f ) );
		if ( active ) Paint.SetPen( pen );

		Paint.DrawText( LocalRect.Shrink( 0, 0, 0, 0 ), Text, TextFlag.Center );
	}

	void OpenContextMenu()
	{
		var m = scene.CreateContextMenu();

		m.AddSeparator();
		m.AddOption( "Close", "close", () => EditorScene.CloseScene( scene ) );

		m.OpenAtCursor();
	}
}

