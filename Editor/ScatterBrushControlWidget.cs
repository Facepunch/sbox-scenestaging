namespace Editor;

/// <summary>
/// Custom control widget for ScatterBrush resource properties
/// </summary>
[CustomEditor( typeof( ScatterBrush ) )]
public class ScatterBrushControlWidget : ResourceControlWidget
{
	public ScatterBrushControlWidget( SerializedProperty property ) : base( property )
	{
	}

	protected override void PaintControl()
	{
		base.PaintControl();

		// Add a badge showing layer count
		var brush = SerializedProperty.GetValue<ScatterBrush>( null );
		if ( brush != null && brush.Layers.Count > 0 )
		{
			var rect = new Rect( Width - 30, Height - 20, 25, 15 );
			Paint.SetBrush( Theme.Blue.WithAlpha( 0.8f ) );
			Paint.ClearPen();
			Paint.DrawRect( rect, 2 );

			Paint.SetPen( Color.White );
			Paint.SetDefaultFont( 7 );
			Paint.DrawText( rect, $"{brush.Layers.Count}", TextFlag.Center );
		}
	}
}
