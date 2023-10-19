using System;
using Editor.NodeEditor;
using Facepunch.ActionJigs;
using static Editor.NodeEditor.GraphView;

namespace Editor.ActionJigs;

public class Properties : Widget
{
	private object _target;

	public object Target
	{
		get => _target;
		set
		{
			if ( _target == value )
			{
				return;
			}

			_target = value;
			_contentInvalid = true;
		}
	}

	private readonly Layout _content;
	private bool _contentInvalid;

	public Properties( Widget parent ) : base( parent )
	{
		Name = "Properties";
		WindowTitle = "Properties";
		SetWindowIcon( "edit" );

		Layout = Layout.Column();

		SetSizeMode( SizeMode.Default, SizeMode.CanShrink );

		_content = Layout.AddColumn();
		Layout.AddStretchCell();
	}


	[EditorEvent.Frame]
	public void Frame()
	{
		if ( Target is ActionNode node )
		{
			node.MarkDirty();
		}

		if ( _contentInvalid )
		{
			_contentInvalid = false;
			RebuildContent();
		}
	}

	private static HashSet<string> HidePropertiesFor { get; } = new()
	{
		"event",
		"property.get",
		"property.set",
		"variable.get",
		"variable.set"
	};

	void RebuildContent()
	{
		_content.Clear( true );

		if ( _target == null )
		{
			return;
		}

		var ps = new ControlSheet();

		if ( Target is ActionNode node && !HidePropertiesFor.Contains( node.Definition.Identifier ) )
		{
			foreach ( var (name, property) in node.Node.Properties )
			{
				if ( name.StartsWith( "_" ) )
				{
					continue;
				}

				var prop = new SerializedNodeParameter<Node.Property, PropertyDefinition>( property );

				ps.AddRow( prop );
			}
		}

		_content.Add( ps );
	}
}

file class SerializedNodeParameter<T, TDef> : SerializedProperty
	where T : Node.ValueParameter<TDef>
	where TDef : class, IValueParameterDefinition
{
	public T Target { get; }

	public override Type PropertyType => Target.Type;
	public override string Name => Target.Name;
	public override string GroupName => typeof(T) == typeof(Node.Property) ? "Properties" : "Inputs";
	public override string DisplayName => Target.Display.Title;
	public override string Description => Target.Display.Description;

	public SerializedNodeParameter( T target )
	{
		Target = target;
	}

	public override bool TryGetAsObject( out SerializedObject obj )
	{
		obj = null;

		var description = EditorTypeLibrary.GetType( PropertyType );

		if ( description == null )
		{
			return false;
		}

		try
		{
			if ( !PropertyType.IsValueType )
			{
				var curVal = GetValue<object>();

				if ( curVal == null )
				{
					return false;
				}

				obj = EditorTypeLibrary.GetSerializedObject( curVal );
				return true;
			}

			obj = EditorTypeLibrary.GetSerializedObject( () => Target.Value ?? (!Target.Definition.IsRequired
				? Target.Definition.Default
				: PropertyType.IsValueType
					? Activator.CreateInstance( PropertyType )
					: null), description, this );
			return true;
		}
		catch
		{
			obj = null;
			return false;
		}
	}

	public override void SetValue<TVal>( TVal value )
	{
		Target.Value = value;
	}

	public override TVal GetValue<TVal>( TVal defaultValue = default )
	{
		return Target.Value is TVal value
			? value
			: Target.Definition is { IsRequired: false, Default: TVal defaultDef }
				? defaultDef : defaultValue;
	}
}

[CustomEditor( typeof( Type ) )]
internal class TypeControlWidget : ControlWidget
{
	public IReadOnlyList<Type> TypeConstraints =>
		(SerializedProperty as SerializedNodeParameter<Node.Property, PropertyDefinition>)
			?.Target.Definition.TypeConstraints;

	public override bool IsControlButton => true;
	public override bool IsControlHovered => base.IsControlHovered || _menu.IsValid();

	Menu _menu;

	public TypeControlWidget( SerializedProperty property ) : base( property )
	{
		Cursor = CursorShape.Finger;

		Layout = Layout.Row();
		Layout.Spacing = 2;
	}

	protected override void PaintControl()
	{
		var value = SerializedProperty.GetValue<Type>();

		var color = IsControlHovered ? Theme.Blue : Theme.ControlText;
		var rect = LocalRect;

		rect = rect.Shrink( 8, 0 );

		Paint.SetPen( color );
		Paint.DrawText( rect, value?.Name ?? "None", TextFlag.LeftCenter );

		Paint.SetPen( color );
		Paint.DrawIcon( rect, "Arrow_Drop_Down", 17, TextFlag.RightCenter );

	}

	protected override void OnMousePress( MouseEvent e )
	{
		base.OnMousePress( e );

		if ( e.LeftMouseButton && !_menu.IsValid() )
		{
			OpenMenu();
		}
	}

	private IEnumerable<TypeDescription> GetPossibleTypes()
	{
		var constraints = TypeConstraints ?? Array.Empty<Type>();

		var componentTypes = EditorTypeLibrary.GetTypes<BaseComponent>();

		foreach ( var typeDesc in componentTypes )
		{
			if ( typeDesc.IsStatic ) continue;
			if ( typeDesc.IsGenericType ) continue;
			if ( constraints.Any( x => !x.IsAssignableFrom( typeDesc.TargetType ) ) ) continue;

			yield return typeDesc;
		}
	}
	
	void OpenMenu()
	{
		var types = GetPossibleTypes().ToArray();

		_menu = new Menu();
		_menu.DeleteOnClose = true;

		var w = new Widget( null );
		w.Layout = Layout.Row();
		w.Layout.Margin = 6;
		w.Layout.Spacing = 4;

		var filter = new Filter( w );
		filter.TextChanged += ( s ) => PopulateTypeMenu( _menu, types, s );
		filter.PlaceholderText = "Filter Types..";

		w.Layout.Add( new Label( "Filter:", w ) );
		w.Layout.Add( filter );

		_menu.AddWidget( w );

		filter.Focus();

		_menu.AboutToShow += () => PopulateTypeMenu( _menu, types );

		_menu.OpenAtCursor( true );
		_menu.MinimumWidth = ScreenRect.Width;
	}

	private void PopulateTypeMenu( Menu menu, IEnumerable<TypeDescription> types, string filter = null )
	{
		menu.RemoveMenus();
		menu.RemoveOptions();

		const int maxFiltered = 10;

		var useFilter = !string.IsNullOrEmpty( filter );
		var truncated = 0;

		if ( useFilter )
		{
			var filtered = types.Where( x => x.FullName.Contains( filter, StringComparison.OrdinalIgnoreCase ) ).ToArray();

			if ( filtered.Length > maxFiltered + 1 )
			{
				truncated = filtered.Length - maxFiltered;
				types = filtered.Take( maxFiltered );
			}
			else
			{
				types = filtered;
			}
		}

		types = types
			.OrderBy( x => string.IsNullOrEmpty( x.Group ) ? x.Namespace ?? "Global" : x.Group )
			.ThenBy( x => x.Name );

		foreach ( var typeDescription in types )
		{
			// Ignore compiler generated names
			if ( typeDescription.Name.StartsWith( "<" ) ) continue;

			var categoryMenu = menu;
			var fullName = string.IsNullOrEmpty( typeDescription.Group )
				? $"{typeDescription.Namespace ?? "Global"}.{typeDescription.Name}"
				: $"{typeDescription.Group}.{typeDescription.Name}";
			var name = fullName;

			if ( !useFilter )
			{
				var nameParts = fullName.Split( ".", StringSplitOptions.RemoveEmptyEntries );

				foreach ( var part in nameParts[..^1] )
				{
					categoryMenu = categoryMenu.FindOrCreateMenu( part );
				}

				name = nameParts[^1];
			}

			var option = categoryMenu.AddOption( name, typeDescription.Icon );
			option.StatusText = typeDescription.Description;
			option.Triggered += () => SerializedProperty.SetValue( typeDescription.TargetType );
		}

		if ( truncated > 0 )
		{
			menu.AddOption( $"...and {truncated} more" );
		}

		menu.AdjustSize();
		menu.Update();
	}
}
