using System.Linq;

namespace Editor.MovieMaker;

#nullable enable

public class IconComboBox<T> : IconButton
	where T : struct, Enum
{
	private T _value;

	public T Value
	{
		get => _value;
		set
		{
			if ( Equals( _value, value ) ) return;

			_value = value;

			Update();
		}
	}

	public IconComboBox() : base( "" )
	{
		FixedWidth = FixedHeight * 1.5f;
	}

	protected virtual IEnumerable<T> OnGetOptions() => Enum.GetValues<T>().Where( x => Convert.ToInt64( x ) >= 0 );

	private static EnumDescription.Entry GetEntry( T option )
	{
		var typeDesc = EditorTypeLibrary.GetEnumDescription( typeof( T ) );
		return typeDesc.GetEntry( option );
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

public class FunctionSelector<T> : IconComboBox<T>
	where T : struct, Enum
{
	private readonly Func<T, Func<float, float>?> _getFunc;

	public FunctionSelector( string title, Func<T, Func<float, float>?> getFunc )
	{
		ToolTip = title;

		_getFunc = getFunc;
	}

	protected override void OnPaintOptionIcon( T option, Rect rect )
	{
		if ( _getFunc( option ) is { } func )
		{
			Paint.SetPen( Paint.Pen, size: 2f );
			Paint.DrawLine( BuildIconPath( func, rect ) );
		}
		else
		{
			Paint.DrawIcon( rect, "question_mark", IconSize );
		}
	}

	private static IEnumerable<Vector2> BuildIconPath( Func<float, float> func, Rect rect )
	{
		const int steps = 16;

		yield return rect.BottomLeft;

		for ( var i = 1; i <= steps; ++i )
		{
			var t = (float)i / steps;
			var x = rect.Left + t * rect.Width;
			var y = rect.Bottom - func( t ) * rect.Height;

			yield return new Vector2( x, y );
		}

		yield return rect.TopRight;
	}
}
