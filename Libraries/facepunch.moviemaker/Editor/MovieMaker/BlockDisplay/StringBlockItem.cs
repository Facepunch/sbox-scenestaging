namespace Editor.MovieMaker.BlockDisplays;

#nullable enable

public sealed class StringBlockItem : PropertyBlockItem<string?>
{
	protected override void OnPaint()
	{
		base.OnPaint();

		if ( Block.GetValue( Block.TimeRange.Start ) is { } value )
		{
			Paint.SetPen( Color.White.WithAlpha( 0.5f ), 12f );
			Paint.DrawText( LocalRect, value );
		}
	}
}
