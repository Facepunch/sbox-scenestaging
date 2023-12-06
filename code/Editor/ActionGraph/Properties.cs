using System;
using System.Text.Json.Serialization;
using Facepunch.ActionGraphs;
using static Sandbox.PhysicsContact;

namespace Editor.ActionGraphs;

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

	private static bool CanEditInputType( Type type )
	{
		if ( type.IsAbstract || type.IsInterface || type.IsArray || type.ContainsGenericParameters )
		{
			return false;
		}

		if ( type == typeof(Variable) )
		{
			return false;
		}

		return true;
	}

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
		else if ( Target is ActionNode node )
		{
			foreach ( var (name, property) in node.Node.Properties )
			{
				if ( name.StartsWith( "_" ) || name == "graph" && node.Definition.Identifier == "graph" )
				{
					continue;
				}

				if ( name == "parameters" )
				{
					var count = (property.Value as Array)?.Length ?? 0;
					var isInput = node.Definition.Identifier == "input";

					for ( var i = 0; i < count; ++i )
					{
						var layout = Layout.Row();
						var title = new Label( isInput ? $"Input #{i + 1}" : $"Output #{i + 1}" );
						title.MinimumHeight = Theme.RowHeight;
						title.Alignment = TextFlag.LeftCenter;
						title.SetStyles( "color: #ddd; font-weight: bold;" );
						layout.Add( title );

						ps.AddLayout( layout );

						var obj = EditorTypeLibrary.GetSerializedObject( isInput
							? new InputParameterProxy( node.Node, i )
							: new OutputParameterProxy( node.Node, i ) );

						obj.OnPropertyChanged += _ => node.MarkDirty();

						ps.AddObject( obj );
					}

					continue;
				}

				var prop = new SerializedNodeParameter<Node.Property, PropertyDefinition>( property );

				ps.AddRow( prop );
			}

			foreach ( var (name, input) in node.Node.Inputs )
			{
				if ( input.IsLinked || input.Name.StartsWith( "_" ) )
				{
					continue;
				}

				if ( !CanEditInputType( input.Type ) )
				{
					continue;
				}

				var prop = new SerializedNodeParameter<Node.Input, InputDefinition>( input );

				ps.AddRow( prop );
			}
		}
		else if ( Target is EditorActionGraph )
		{
			ps.AddObject( EditorTypeLibrary.GetSerializedObject( Target ) );
		}

		_content.Add( ps );
	}
}

internal abstract class InputOutputParameterProxy<T>
{
	[HideInEditor]
	public Node Target { get; }

	[HideInEditor]
	public int Index { get; }

	[HideInEditor]
	protected abstract ParameterDisplayInfo DisplayInfo { get; set; }

	[HideInEditor]
	protected T[] Parameters
	{
		get
		{
			return Target.Properties["parameters"].Value as T[] ?? Array.Empty<T>();
		}
		set
		{
			Target.Properties["parameters"].Value = value;
		}
	}

	public string Title
	{
		get
		{
			return DisplayInfo?.Title;
		}
		set
		{
			var displayInfo = DisplayInfo;
			DisplayInfo = displayInfo == null
				? new ParameterDisplayInfo( value )
				: displayInfo with { Title = value };
		}
	}

	public string Description
	{
		get
		{
			return DisplayInfo?.Description;
		}
		set
		{
			var displayInfo = DisplayInfo ?? new ParameterDisplayInfo( "Unnamed" );
			DisplayInfo = displayInfo with { Description = value };
		}
	}

	protected InputOutputParameterProxy( Node target, int index )
	{
		Target = target;
		Index = index;
	}
}

internal class InputParameterProxy : InputOutputParameterProxy<InputParameter>
{
	[HideInEditor]
	protected override ParameterDisplayInfo DisplayInfo
	{
		get
		{
			return Parameters[Index].Display;
		}
		set
		{
			var parameters = Parameters.ToArray();
			parameters[Index] = parameters[Index] with { Display = value };
			Parameters = parameters;
		}
	}

	public InputParameterProxy( Node target, int index ) : base( target, index )
	{
	}
}

internal class OutputParameterProxy : InputOutputParameterProxy<OutputParameter>
{
	[HideInEditor]
	protected override ParameterDisplayInfo DisplayInfo
	{
		get
		{
			return Parameters[Index].Display;
		}
		set
		{
			var parameters = Parameters.ToArray();
			parameters[Index] = parameters[Index] with { Display = value };
			Parameters = parameters;
		}
	}

	public OutputParameterProxy( Node target, int index ) : base( target, index )
	{
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
