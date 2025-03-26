using System.Globalization;
using System.Linq;
using Sandbox.UI;

namespace Editor.MovieMaker;

#nullable enable

public sealed class ToolbarWidget : Widget
{
	private readonly List<ToolbarGroup> _groups = new();

	public ToolbarWidget( MovieEditorPanel parent ) : base( parent )
	{
		Parent = parent;

		Layout = Layout.Row();
		Layout.Spacing = 4f;
		Layout.Margin = new Margin( 0f, 4f );

		VerticalSizeMode = SizeMode.CanShrink;
	}

	public ToolbarGroup AddGroup( bool permanent = false, bool alignRight = false )
	{
		var group = new ToolbarGroup( this ) { IsPermanent = permanent, AlignRight = alignRight };

		_groups.Add( group );

		UpdateLayout();

		return group;
	}

	internal void RemoveGroup( ToolbarGroup group )
	{
		_groups.Remove( group );

		UpdateLayout();
	}

	public void Reset()
	{
		var toRemove = _groups.Where( x => !x.IsPermanent ).ToArray();

		foreach ( var group in toRemove )
		{
			group.Destroy();
		}
	}

	private void UpdateLayout()
	{
		Layout.Clear( false );

		_groups.RemoveAll( x => !x.IsValid );

		foreach ( var group in _groups.Where( x => !x.AlignRight ) )
		{
			Layout.Add( group );
		}

		if ( !_groups.Any( x => x.AlignRight ) ) return;

		Layout.AddStretchCell();

		foreach ( var group in _groups.Where( x => x.AlignRight ) )
		{
			Layout.Add( group );
		}
	}

	protected override void OnPaint()
	{
		Paint.SetBrushAndPen( Theme.WidgetBackground );
		Paint.DrawRect( LocalRect );
	}
}

public sealed class ToolbarGroup : Widget
{
	public bool IsPermanent { get; init; }
	public bool AlignRight { get; init; }

	public ToolbarGroup( ToolbarWidget parent )
		: base( parent )
	{
		HorizontalSizeMode = SizeMode.CanGrow;

		Layout = Layout.Row();
		Layout.Spacing = 2f;
		Layout.Margin = 4f;
	}

	public override void OnDestroyed()
	{
		if ( !IsValid ) return;

		if ( Parent is ToolbarWidget { IsValid: true } toolbar )
		{
			toolbar.RemoveGroup( this );
		}
	}

	protected override void OnPaint()
	{
		Paint.ClearPen();
		Paint.SetBrush( Theme.ControlBackground.LerpTo( Theme.WidgetBackground, 0.5f ) );
		Paint.DrawRect( LocalRect, 3f );
	}

	public IconButton AddAction( string title, string icon, Action action, Func<bool>? enabled = null )
	{
		var btn = new IconButton( icon )
		{
			ToolTip = title,
			IconSize = 16
		};

		btn.OnClick += action;

		if ( enabled != null )
		{
			btn.Bind( nameof( IconButton.Enabled ) )
				.ReadOnly()
				.From( enabled, (Action<bool>?)null );
		}

		Layout.Add( btn );

		return btn;
	}

	public IconButton AddToggle( string title, string icon, Func<bool> getState, Action<bool> setState, bool background = true )
	{
		var btn = new IconButton( icon )
		{
			ToolTip = title,
			IconSize = 16,
			IsToggle = true
		};

		if ( !background )
		{
			btn.Background = Color.Transparent;
			btn.BackgroundActive = Color.Transparent;
			btn.ForegroundActive = Theme.Primary;
		}

		btn.Bind( "IsActive" ).From( getState, setState );

		Layout.Add( btn );

		return btn;
	}

	public InterpolationSelector AddInterpolationSelector( Func<InterpolationMode> getValue, Action<InterpolationMode> setValue )
	{
		var selector = new InterpolationSelector();

		selector.Bind( "Value" ).From( getValue, setValue );

		Layout.Add( selector );

		return selector;
	}

	public (FloatSlider Slider, Label Label) AddSlider( string title, Func<float> getValue, Action<float> setValue, float minimum = 0f,
		float maximum = 1f, float step = 0.01f, Func<string>? getLabel = null )
	{
		var slider = new FloatSlider( null )
		{
			ToolTip = title,
			FixedWidth = 80f,
			Minimum = minimum,
			Maximum = maximum,
			Step = step
		};

		slider.Bind( nameof( FloatSlider.Value ) )
			.From( getValue, setValue );

		Layout.Add( slider );

		var label = new Label( null )
		{
			Color = Color.White.Darken( 0.5f ),
			Margin = 4f,
			Alignment = TextFlag.Left
		};

		label.Bind( nameof( Label.Text ) )
			.ReadOnly()
			.From( getLabel ?? (() => slider.Value.ToString( CultureInfo.InvariantCulture )), (Action<string>?)null );

		Layout.Add( label );

		return (slider, label);
	}

	public void AddSpacingCell() => Layout.AddSpacingCell( 8f );
}
