using System.Linq;

namespace Editor.MovieMaker;

#nullable enable

public class IconComboBox<T> : IconButton
	where T : struct, Enum
{
	public T Value { get; set; }

	public IconComboBox() : base( "" )
	{
		FixedWidth = FixedHeight * 1.5f;
	}

	protected virtual IEnumerable<T> OnGetOptions() => Enum.GetValues<T>();

	private static EnumDescription.Entry GetEntry( T option )
	{
		// This is shit, why doesn't EnumDescription.GetEntry(object) work??

		var intValue = (long)Convert.ChangeType( option, typeof(long) );
		var typeDesc = EditorTypeLibrary.GetEnumDescription( typeof( T ) );
		return typeDesc.FirstOrDefault( x => x.IntegerValue == intValue );
	}

	protected virtual string OnGetOptionTitle( T option )
	{
		var entry = GetEntry( option );

		return entry.Title ?? entry.Name.ToTitleCase();
	}

	protected virtual void OnPaintOptionIcon( T option, Rect rect )
	{
		var entry = GetEntry( option );

		Paint.DrawIcon( rect, entry.Icon, IconSize );
	}

	protected override void OnMousePress( MouseEvent e )
	{
		e.Accepted = true;

		IsActive = true;

		var menu = new Menu( this );

		foreach ( var value in OnGetOptions() )
		{
			var option = menu.AddOption( OnGetOptionTitle( value ), action: () => Value = value );

			option.Checkable = true;
			option.Checked = Equals( Value, value );
		}

		menu.AboutToHide += () =>
		{
			IsActive = false;
		};

		menu.OpenAt( ScreenRect.BottomLeft );
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

		fg = Enabled
			? fg.WithAlphaMultiplied( Paint.HasMouseOver ? 1.0f : 0.7f )
			: fg.WithAlphaMultiplied( 0.25f );


		var iconRect = LocalRect.Contain( FixedHeight, TextFlag.LeftCenter ).Contain( IconSize );
		var arrowRect = LocalRect.Contain( FixedHeight * 0.625f, TextFlag.RightCenter ).Contain( IconSize );

		Paint.SetPen( fg );
		OnPaintOptionIcon( Value, iconRect );

		Paint.SetPen( fg );
		Paint.DrawIcon( arrowRect, "expand_more", IconSize );
	}
}

public class InterpolationSelector : IconComboBox<InterpolationMode>
{
	public InterpolationSelector()
	{
		ToolTip = "Interpolation Mode";
	}

	protected override void OnPaintOptionIcon( InterpolationMode option, Rect rect )
	{
		Paint.SetPen( Paint.Pen, size: 2f );
		Paint.DrawLine( BuildIconPath( Value, rect ) );
	}

	private static IEnumerable<Vector2> BuildIconPath( InterpolationMode mode, Rect rect )
	{
		const int steps = 16;

		yield return rect.BottomLeft;

		for ( var i = 1; i <= steps; ++i )
		{
			var t = (float)i / steps;
			var x = rect.Left + t * rect.Width;
			var y = rect.Bottom - mode.Apply( t ) * rect.Height;

			yield return new Vector2( x, y );
		}

		yield return rect.TopRight;
	}
}
