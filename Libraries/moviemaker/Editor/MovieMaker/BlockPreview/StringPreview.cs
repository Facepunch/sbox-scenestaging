namespace Editor.MovieMaker.BlockPreviews;

#nullable enable

public sealed class StringPreview : BlockPreview<string>
{
	protected override void OnPaint()
	{
		base.OnPaint();

		if ( Constant?.Value is { } value )
		{
			Paint.SetPen( Color.White.WithAlpha( 0.5f ), 12f );
			Paint.DrawText( LocalRect, value );
		}
	}
}
