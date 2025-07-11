﻿using System.Linq;

namespace Editor.MovieMaker;

#nullable enable

public abstract class IconComboBox<T> : IconButton
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

	private float? _iconAspect;

	public float? IconAspect
	{
		get => _iconAspect;
		set
		{
			_iconAspect = value;

			if ( value is { } aspect )
			{
				var margin = (FixedHeight - IconSize) * 0.5f;

				// Add 0.5 for the dropdown arrow

				FixedWidth = IconSize * (aspect + 0.5f) + margin * 2.5f;
			}
			else
			{
				MinimumSize = IconSize;
				MaximumSize = 65536f;
			}
		}
	}

	public IconComboBox() : base( "" )
	{
		IconSize = 10f;
		IconAspect = 1f;
	}

	protected abstract IEnumerable<T> OnGetOptions();

	protected abstract string OnGetOptionTitle( T option );

	protected abstract void OnPaintOptionIcon( T option, Rect rect );

	protected override void OnMousePress( MouseEvent e )
	{
		e.Accepted = true;

		IsActive = true;

		var menu = new Menu( this );

		OnCreateMenu( menu );

		menu.AboutToHide += () =>
		{
			IsActive = false;
		};

		menu.OpenAt( ScreenRect.BottomLeft );
	}

	protected virtual void OnCreateMenu( Menu menu )
	{
		foreach ( var value in OnGetOptions() )
		{
			var option = menu.AddOption( OnGetOptionTitle( value ), action: () => Value = value );

			option.Checkable = true;
			option.Checked = Equals( Value, value );
		}
	}

	protected override void OnPaint()
	{
		Paint.Antialiasing = true;

		Paint.ClearBrush();
		Paint.ClearPen();

		bool active = Enabled && IsActive;

		var bg = active ? BackgroundActive : Background;
		var fg = active ? ForegroundActive : Foreground;

		Paint.SetBrush( bg.WithAlphaMultiplied( Enabled ? 1f : 0.25f ) );
		Paint.DrawRect( LocalRect, 2.0f );

		Paint.ClearBrush();
		Paint.ClearPen();

		fg = Enabled
			? fg.WithAlphaMultiplied( Paint.HasMouseOver ? 1.0f : 0.7f )
			: fg.WithAlphaMultiplied( 0.25f );

		var iconSize = IconAspect is { } aspect
			? new Vector2( IconSize * aspect, IconSize )
			: new Vector2( Width - IconSize * 0.5f, IconSize );

		var iconRect = LocalRect.Shrink( 0f, 0f, 0.5f * FixedHeight, 0f ).Contain( iconSize );
		var arrowRect = LocalRect.Contain( FixedHeight * 0.625f, TextFlag.RightCenter ).Contain( IconSize );

		Paint.SetPen( fg );
		OnPaintOptionIcon( Value, iconRect );

		Paint.SetPen( fg );
		Paint.DrawIcon( arrowRect, "expand_more", IconSize );
	}
}

public class EnumIconComboBox<T> : IconComboBox<T>
	where T : struct, Enum
{
	protected override IEnumerable<T> OnGetOptions() => Enum.GetValues<T>().Where( x => Convert.ToInt64( x ) >= 0 );

	private static EnumDescription.Entry GetEntry( T option )
	{
		var typeDesc = EditorTypeLibrary.GetEnumDescription( typeof( T ) );
		return typeDesc.GetEntry( option );
	}

	protected override string OnGetOptionTitle( T option )
	{
		var entry = GetEntry( option );

		return entry.Title ?? entry.Name.ToTitleCase();
	}

	protected override void OnPaintOptionIcon( T option, Rect rect )
	{
		var entry = GetEntry( option );

		Paint.DrawIcon( rect, entry.Icon, IconSize );
	}
}

public class FunctionSelector<T> : EnumIconComboBox<T>
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
