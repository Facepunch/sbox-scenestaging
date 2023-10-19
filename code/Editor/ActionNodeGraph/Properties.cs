using System;
using System.IO;
using System.Reflection;
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
		else if ( Target is CommentNode commentNode )
		{
			var obj = EditorTypeLibrary.GetSerializedObject( commentNode );

			obj.OnPropertyChanged += _ => commentNode.Update();

			ps.AddObject( obj );
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
	public Type GenericParameter =>
		(SerializedProperty as SerializedNodeParameter<Node.Property, PropertyDefinition>)
			?.Target.Definition.GenericParameter;

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

	private static HashSet<Type> SystemTypes { get; } = new()
	{
		typeof(int),
		typeof(float),
		typeof(string),
		typeof(bool),
		typeof(GameObject),
		typeof(GameTransform),
		typeof(Color),
		typeof(Vector2),
		typeof(Vector3),
		typeof(Vector4),
		typeof(Angles),
		typeof(Rotation)
	};

	private TypeOption GetTypeOption( Type type )
	{
		var typeDesc = EditorTypeLibrary.GetType( type );

		var path = typeDesc is { }
			? GetTypePath( typeDesc )
			: $"System/{type.Name}";

		return new TypeOption( path,
			path.Split( "/" ),
			type,
			type.Name,
			typeDesc?.Description,
			typeDesc?.Icon );
	}

	private string GetTypePath( TypeDescription typeDesc )
	{
		var prefix = "Other";

		if ( typeDesc.TargetType.IsAssignableTo( typeof(Resource) ) )
		{
			prefix = "Resource";
		}
		else if ( typeDesc.TargetType.IsAssignableTo( typeof(BaseComponent) ) )
		{
			prefix = "Component";
		}
		else if ( SystemTypes.Contains( typeDesc.TargetType ) )
		{
			prefix = typeDesc.Namespace ?? "Sandbox";
		}

		if ( !string.IsNullOrEmpty( typeDesc.Group ) )
		{
			prefix += $"/{typeDesc.Group}";
		}
		else if ( prefix == "Other" && !string.IsNullOrEmpty( typeDesc.Namespace ) )
		{
			prefix += $"/{typeDesc.Namespace}";
		}

		return $"{prefix}/{typeDesc.Name}";
	}

	private record TypeOption( string Path, string[] PathParts, Type Type, string Title, string Description, string Icon );

	private static bool SatisfiesConstraints( Type type, Type genericParam )
	{
		if ( genericParam is null )
		{
			return true;
		}

		var attribs = genericParam.GenericParameterAttributes;

		if ( attribs.HasFlag( GenericParameterAttributes.ReferenceTypeConstraint ) )
		{
			if ( type.IsValueType ) return false;
		}

		if ( attribs.HasFlag( GenericParameterAttributes.NotNullableValueTypeConstraint ) )
		{
			if ( !type.IsValueType ) return false;
		}

		if ( attribs.HasFlag( GenericParameterAttributes.DefaultConstructorConstraint ) )
		{
			if ( type.GetConstructor( BindingFlags.Public, Array.Empty<Type>() ) is null )
			{
				return false;
			}
		}

		// TODO: constraints might involve other generic parameters

		foreach ( var constraint in genericParam.GetGenericParameterConstraints() )
		{
			if ( !type.IsAssignableTo( constraint ) )
			{
				return false;
			}
		}

		return true;
	}

	private IEnumerable<TypeOption> GetPossibleTypes()
	{
		var genericParam = GenericParameter;

		foreach ( var type in SystemTypes )
		{
			if ( !SatisfiesConstraints( type, genericParam ) ) continue;
			yield return GetTypeOption( type );
		}

		var componentTypes = EditorTypeLibrary.GetTypes<BaseComponent>();
		var resourceTypes = EditorTypeLibrary.GetTypes<GameResource>();

		foreach ( var typeDesc in componentTypes.Concat( resourceTypes ) )
		{
			if ( typeDesc.IsStatic ) continue;
			if ( typeDesc.IsGenericType ) continue;
			if ( !SatisfiesConstraints( typeDesc.TargetType, genericParam ) ) continue;

			var path = GetTypePath( typeDesc );

			yield return new TypeOption( path,
				path.Split( "/" ),
				typeDesc.TargetType,
				typeDesc.Name,
				typeDesc.Description,
				typeDesc.Icon );
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

	private void PopulateTypeMenu( Menu menu, IEnumerable<TypeOption> types, string filter = null )
	{
		menu.RemoveMenus();
		menu.RemoveOptions();

		const int maxFiltered = 10;

		var useFilter = !string.IsNullOrEmpty( filter );
		var truncated = 0;

		if ( useFilter )
		{
			var filtered = types.Where( x => x.Type.Name.Contains( filter, StringComparison.OrdinalIgnoreCase ) ).ToArray();

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
			.OrderBy( x => x.Path );

		foreach ( var type in types )
		{
			var categoryMenu = menu;

			if ( !useFilter )
			{
				foreach ( var part in type.PathParts[..^1] )
				{
					categoryMenu = categoryMenu.FindOrCreateMenu( part );
				}
			}

			var option = categoryMenu.AddOption( type.Title, type.Icon );
			option.StatusText = type.Description;
			option.Triggered += () => SerializedProperty.SetValue( type.Type );
		}

		if ( truncated > 0 )
		{
			menu.AddOption( $"...and {truncated} more" );
		}

		menu.AdjustSize();
		menu.Update();
	}
}
