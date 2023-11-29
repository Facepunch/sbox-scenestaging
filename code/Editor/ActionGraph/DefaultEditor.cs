using Editor.NodeEditor;

namespace Editor.ActionGraph;

public class DefaultEditor : ValueEditor
{
	public string Title { get; set; }
	public object Value { get; set; }

	public NodeUI Node { get; set; }

	public DefaultEditor( GraphicsItem parent ) : base( parent )
	{
		HoverEvents = true;
		Cursor = CursorShape.Finger;
	}

	protected override void OnPaint()
	{
		if ( !Enabled )
			return;

		Paint.Antialiasing = true;
		Paint.TextAntialiasing = true;

		var bg = Theme.ControlBackground.WithAlpha( 0.4f );
		var fg = Theme.ControlText;

		var rect = LocalRect.Shrink( 1 );

		Paint.ClearPen();
		Paint.SetBrush( bg );
		Paint.DrawRect( rect, 2 );

		var shrink = 10f;
		
		if ( !string.IsNullOrWhiteSpace( Title ) )
		{
			Paint.DrawText( rect.Shrink( shrink, 0, shrink, 0 ), Title, TextFlag.LeftCenter );
		}

		Paint.DrawText( rect.Shrink( shrink, 0, shrink, 0 ), $"{Value:0.000}", TextFlag.RightCenter );
	}
}
