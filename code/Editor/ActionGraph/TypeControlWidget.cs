using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Editor.NodeEditor;
using Facepunch.ActionGraphs;

namespace Editor.ActionGraph;

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
		if ( typeDesc.TargetType.DeclaringType != null )
		{
			return $"{GetTypePath( EditorTypeLibrary.GetType( typeDesc.TargetType.DeclaringType ) )}/{typeDesc.Name}";
		}

		var prefix = "Other";

		if ( typeDesc.TargetType.IsAssignableTo( typeof( Resource ) ) )
		{
			prefix = "Resource";
		}
		else if ( typeDesc.TargetType.IsAssignableTo( typeof( BaseComponent ) ) )
		{
			prefix = "Component";
		}
		else if ( SystemTypes.Contains( typeDesc.TargetType ) )
		{
			prefix = typeDesc.Namespace?.Replace( '.', '/' ) ?? "Sandbox";
		}

		if ( !string.IsNullOrEmpty( typeDesc.Group ) )
		{
			prefix += $"/{typeDesc.Group}";
		}
		else if ( prefix == "Other" && !string.IsNullOrEmpty( typeDesc.Namespace ) )
		{
			prefix += $"/{typeDesc.Namespace.Replace( '.', '/' )}";
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
		var listedTypes = new HashSet<Type>();

		foreach ( var type in SystemTypes )
		{
			if ( !listedTypes.Add( type ) ) continue;
			if ( !SatisfiesConstraints( type, genericParam ) ) continue;
			yield return GetTypeOption( type );
		}

		var componentTypes = EditorTypeLibrary.GetTypes<BaseComponent>();
		var resourceTypes = EditorTypeLibrary.GetTypes<GameResource>();
		var userTypes = EditorTypeLibrary.GetTypes()
			.Where( x => x.TargetType.Assembly.GetName().Name?.StartsWith( "package." ) ?? false );

		foreach ( var typeDesc in componentTypes.Concat( resourceTypes ).Concat( userTypes ) )
		{
			if ( typeDesc.IsStatic ) continue;
			if ( typeDesc.IsGenericType ) continue;
			if ( typeDesc.HasAttribute<CompilerGeneratedAttribute>() ) continue;
			if ( typeDesc.Name.StartsWith( "<" ) || typeDesc.Name.StartsWith( "_" ) ) continue;
			if ( !listedTypes.Add( typeDesc.TargetType ) ) continue;
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

		var filter = new GraphView.Filter( w );
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
