namespace Editor.MovieMaker;

#nullable enable

public class InterpolationButton : IconButton
{
	public InterpolationMode Value { get; }

	public InterpolationButton( InterpolationMode value, Action? onClick = null, Widget? parent = null )
		: base( "", onClick, parent )
	{
		Value = value;
	}

	protected override void OnPaint()
	{
		Paint.Antialiasing = true;

		Paint.ClearBrush();
		Paint.ClearPen();

		bool active = Enabled && IsActive;

		var bg = active ? BackgroundActive : Background;
		var fg = active ? ForegroundActive : Foreground;

		float alpha = Paint.HasMouseOver ? 0.5f : 0.25f;

		if ( !Enabled )
			alpha = 0.1f;

		Paint.SetBrush( bg.WithAlphaMultiplied( alpha ) );
		Paint.DrawRect( LocalRect, 2.0f );

		Paint.ClearBrush();
		Paint.ClearPen();

		Paint.SetPen(
			Enabled ? fg.WithAlphaMultiplied( Paint.HasMouseOver ? 1.0f : 0.7f ) : fg.WithAlphaMultiplied( 0.25f ),
			size: 2f );

		Paint.DrawLine( BuildLine() );
	}

	private IEnumerable<Vector2> BuildLine()
	{
		const int steps = 16;

		var iconRect = LocalRect.Contain( IconSize );

		yield return iconRect.BottomLeft;

		for ( var i = 1; i <= steps; ++i )
		{
			var t = (float)i / steps;
			var x = iconRect.Left + t * iconRect.Width;
			var y = iconRect.Bottom - Value.Apply( t ) * iconRect.Height;

			yield return new Vector2( x, y );
		}

		yield return iconRect.TopRight;
	}
}
