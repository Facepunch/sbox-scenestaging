using System;
using Editor.NodeEditor;
using Facepunch.ActionGraphs;

namespace Editor.ActionGraphs;

public class DefaultEditor : ValueEditor
{
	public NodeUI Node { get; }
	public Plug Plug { get; }
	public Node.Input Input { get; }

	public DefaultEditor( NodeUI node, Plug parent ) : base( parent )
	{
		HoverEvents = true;
		Cursor = CursorShape.Finger;

		Node = node;
		Plug = parent;
		Input = (parent.Inner as ActionPlug<Node.Input, InputDefinition>)?.Parameter;
	}

	private static string FormatValue( Type type, object value )
	{
		if ( type == typeof(string) )
		{
			return $"\"{value}\"";
		}

		if ( type == typeof(float) || type == typeof(double) )
		{
			return $"{value:F2}";
		}

		return $"{value}";
	}

	protected override void OnPaint()
	{
		Enabled = !Input.IsLinked && Input.Value is not null;

		if ( !Enabled )
			return;

		Paint.Antialiasing = true;
		Paint.TextAntialiasing = true;

		var rect = LocalRect.Shrink( 1 );

		var bg = Theme.ControlBackground;
		var fg = Theme.ControlText;

		if ( !string.IsNullOrWhiteSpace( Input.Display.Title ) )
		{
			Paint.SetPen( fg );
			Paint.DrawText( rect.Shrink( 5f, 0, 5f, 0 ), Input.Display.Title, TextFlag.LeftCenter );
		}
		var shrink = 10f;

		var text = FormatValue( Input.Type, Input.Value );
		var textSize = Paint.MeasureText( text );

		var valueRect = new Rect( LocalRect.Left - textSize.x - shrink * 2, LocalRect.Top, textSize.x + shrink * 2, LocalRect.Height )
			.Shrink( 0f, 2f, 0f, 2f );
		var handleConfig = Plug.HandleConfig;

		Paint.SetPen( handleConfig.Color.Desaturate( 0.2f ).Darken( 0.3f ), 2f );
		Paint.SetBrush( bg );
		Paint.DrawRect( valueRect, 2 );

		Paint.SetPen( fg );
		Paint.DrawText( valueRect.Shrink( shrink, 0, shrink, 0 ), text, TextFlag.RightCenter );

	}
}
