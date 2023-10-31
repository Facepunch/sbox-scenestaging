using System;
using Facepunch.ActionGraphs;

namespace Editor.ActionGraph;

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
		"var.get",
		"var.set"
	};

	void RebuildContent()
	{
		_content.Clear( true );

		if ( _target == null )
		{
			return;
		}

		var ps = new ControlSheet();

		if ( Target is CommentActionNode commentNode )
		{
			var obj = EditorTypeLibrary.GetSerializedObject( commentNode );

			obj.OnPropertyChanged += _ => commentNode.MarkDirty();

			ps.AddObject( obj );
		}
		else if ( Target is ActionNode node && !HidePropertiesFor.Contains( node.Definition.Identifier ) )
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

internal class SerializedNodeParameter<T, TDef> : SerializedProperty
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

			obj = EditorTypeLibrary.GetSerializedObject(
				() => Target.Value ?? (!Target.Definition.IsRequired
					? Target.Definition.Default : Activator.CreateInstance( PropertyType ) ),
				description, this );
			return true;
		}
		catch ( Exception e )
		{
			Log.Warning( e );
			obj = null;
			return false;
		}
	}

	private static object ConvertTo( object value, Type sourceType, Type targetType )
	{
		if ( sourceType == targetType || sourceType.IsAssignableTo( targetType ) )
		{
			return value;
		}

		if ( sourceType == typeof(long) && targetType.IsEnum )
		{
			// Special case for EnumControlWidget :S

			return Enum.ToObject( targetType, (long)value );
		}

		return Convert.ChangeType( value, targetType );
	}

	public override void SetValue<TVal>( TVal value )
	{
		Target.Value = ConvertTo( value, typeof(TVal), PropertyType );
	}

	public override TVal GetValue<TVal>( TVal defaultValue = default )
	{
		if ( PropertyType.IsEnum && typeof(TVal) == typeof(long)
			&& Target.Value is {} enumVal && enumVal.GetType() == PropertyType )
		{
			// Special case for EnumControlWidget :S

			return (TVal)Convert.ChangeType(
				Convert.ChangeType(
					enumVal,
					Enum.GetUnderlyingType( PropertyType ) ),
				typeof(TVal) );
		}

		return Target.Value is TVal value
			? value
			: Target.Definition is { IsRequired: false, Default: TVal defaultDef }
				? defaultDef : defaultValue;
	}
}
