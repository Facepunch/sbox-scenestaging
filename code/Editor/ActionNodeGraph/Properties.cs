using System;
using Facepunch.ActionJigs;

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
	public override string DisplayName => Target.Display.Title ?? Name;
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
