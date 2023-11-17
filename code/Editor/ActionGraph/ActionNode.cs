using System;
using System.Collections;
using System.Text.Json.Serialization;
using Editor.NodeEditor;
using Facepunch.ActionGraphs;
using Sandbox.ActionGraphs;

namespace Editor.ActionGraph;

public record struct ActionNodeType( NodeDefinition Definition ) : INodeType
{
	private static HashSet<string> Hidden { get; } = new ()
	{
		"input", "output",
		"call", "graph",
		"nop", "comment",

		"property.get", "property.set",
		"field.get", "field.set",
		"var.get", "var.set"
	};

	public string Identifier => Definition.Identifier;

	public DisplayInfo DisplayInfo => new DisplayInfo
	{
		Name = Definition.DisplayInfo.Title,
		Description = Definition.DisplayInfo.Description,
		Icon = Definition.DisplayInfo.Icon,
		Tags = Definition.DisplayInfo.Tags,
		Group = Definition.DisplayInfo.Category
	};

	public bool HasInput( Type valueType )
	{
		var isSignal = valueType == typeof(OutputSignal);
		return Definition.Bind( null ).Inputs
			.Any( x => isSignal ? x.IsSignal : x.Type.IsAssignableFrom( valueType ) );
	}

	public bool HideInEditor => Hidden.Contains( Identifier );

	public static ActionNode CreateEditorNode( ActionGraph graph, Node node )
	{
		return node.Definition.Identifier switch
		{
			"comment" => new CommentActionNode( graph, node ),
			"nop" => new RerouteActionNode( graph, node ),
			_ => new ActionNode( graph, node )
		};
	}

	public INode CreateNode( IGraph graph )
	{
		var actionGraph = (ActionGraph)graph;
		var node = actionGraph.Graph.AddNode( Definition );

		return CreateEditorNode( actionGraph, node );
	}
}

public record struct GraphNodeType( ActionGraphResource Resource ) : INodeType
{
	public string Identifier => Resource.ResourcePath;

	public DisplayInfo DisplayInfo => Resource.DisplayInfo;

	public bool HasInput( Type valueType )
	{
		// TODO
		return false;
	}

	public bool HideInEditor => false;

	public INode CreateNode( IGraph graph )
	{
		var actionGraph = (ActionGraph)graph;
		var node = actionGraph.Graph.AddNode( actionGraph.Graph.NodeLibrary.Graph );

		node.Properties["graph"].Value = Resource.ResourcePath;

		return new ActionNode( actionGraph, node );
	}
}

public enum PropertyNodeKind
{
	Get,
	Set
}

public record struct VariableNodeType( string Name, Type Type, PropertyNodeKind Kind, bool Create, bool ReadWrite ) : INodeType
{
	public DisplayInfo DisplayInfo => new()
	{
		Name = ReadWrite ? $"{Name} ({GraphView.FormatTypeName( Type )})/{Kind}" : $"{Name} ({GraphView.FormatTypeName( Type )}, {Kind})",
		Group = "Variables",
		Icon = Create ? "add" : Kind == PropertyNodeKind.Set
			? EditorNodeLibrary.SetVar.DisplayInfo.Icon
			: EditorNodeLibrary.GetVar.DisplayInfo.Icon
	};

	public bool HasInput( Type valueType )
	{
		return Kind == PropertyNodeKind.Set &&
			(valueType == typeof(OutputSignal) || Type.IsAssignableFrom( valueType ));
	}

	public bool HideInEditor => false;

	public INode CreateNode( IGraph graph )
	{
		var actionGraph = (ActionGraph)graph;
		var node = actionGraph.Graph.AddNode( Kind == PropertyNodeKind.Set
			? actionGraph.Graph.NodeLibrary.SetVar
			: actionGraph.Graph.NodeLibrary.GetVar );

		var (name, type) = (Name, Type);

		var variable = Create
			? actionGraph.Graph.AddVariable( Name, Type )
			: actionGraph.Graph.Variables.First( x => x.Name == name && x.Type == type );

		node.Properties["var"].Value = variable;

		return new ActionNode( actionGraph, node );
	}

	public string Identifier => $"var.{Kind}/{Name}";
}

public record struct MethodNodeType( MethodDescription Method ) : INodeType
{
	public DisplayInfo DisplayInfo => new()
	{
		Name = Method.Title,
		Description = Method.Description,
		Group = Method.TypeDescription.Name,
		Icon = Method.Icon ?? (Method.HasAttribute<PureAttribute>() ? "run_circle" : EditorNodeLibrary.CallMethod.DisplayInfo.Icon)
	};

	public bool HasInput( Type valueType )
	{
		return valueType == typeof(OutputSignal)
			|| !Method.IsStatic && Method.TypeDescription.TargetType.IsAssignableFrom( valueType )
			|| Method.Parameters.Any( x => x.ParameterType.IsAssignableFrom( valueType ) );
	}

	public bool HideInEditor => false;

	public INode CreateNode( IGraph graph )
	{
		var actionGraph = (ActionGraph)graph;
		var node = actionGraph.Graph.AddNode( actionGraph.Graph.NodeLibrary.CallMethod );

		node.Properties["_type"].Value = Method.TypeDescription.TargetType;
		node.Properties["_name"].Value = Method.Name;

		return new ActionNode( actionGraph, node );
	}

	public string Identifier => $"call/{Method.TypeDescription.FullName}/{Method.Name}";
}

public record struct PropertyNodeType( PropertyDescription Property, PropertyNodeKind Kind, bool ReadWrite ) : INodeType
{
	public DisplayInfo DisplayInfo => new ()
	{
		Name = ReadWrite ? $"{Property.Title}/{Kind}" : $"{Property.Title} ({Kind})",
		Description = Property.Description,
		Group = Property.TypeDescription.Name,
		Icon = Property.Icon ?? (Kind == PropertyNodeKind.Get
			? EditorNodeLibrary.GetProperty.DisplayInfo.Icon
			: EditorNodeLibrary.SetProperty.DisplayInfo.Icon)
	};

	public bool HasInput( Type valueType )
	{
		return Kind == PropertyNodeKind.Set && valueType == typeof(OutputSignal)
			|| Property.TypeDescription.TargetType.IsAssignableFrom( valueType )
			|| Property.PropertyType.IsAssignableFrom( valueType );
	}

	public bool HideInEditor => false;

	public INode CreateNode( IGraph graph )
	{
		var actionGraph = (ActionGraph)graph;
		var node = actionGraph.Graph.AddNode( Kind switch
		{
			PropertyNodeKind.Set => actionGraph.Graph.NodeLibrary.SetProperty,
			PropertyNodeKind.Get => actionGraph.Graph.NodeLibrary.GetProperty,
			_ => throw new NotImplementedException()
		} );

		node.Properties["_type"].Value = Property.TypeDescription.TargetType;
		node.Properties["_name"].Value = Property.Name;

		return new ActionNode( actionGraph, node );
	}

	public string Identifier => $"property.{Kind}/{Property.TypeDescription.FullName}/{Property.Name}";
}

public record struct FieldNodeType( FieldDescription Field, PropertyNodeKind Kind, bool ReadWrite ) : INodeType
{
	public DisplayInfo DisplayInfo => new()
	{
		Name = ReadWrite ? $"{Field.Title}/{Kind}" : $"{Field.Title} ({Kind})",
		Description = Field.Description,
		Group = Field.TypeDescription.Name,
		Icon = Field.Icon ?? (Kind == PropertyNodeKind.Get
			? EditorNodeLibrary.GetProperty.DisplayInfo.Icon
			: EditorNodeLibrary.SetProperty.DisplayInfo.Icon)
	};

	public bool HasInput( Type valueType )
	{
		return Kind == PropertyNodeKind.Set && valueType == typeof( OutputSignal )
		       || Field.TypeDescription.TargetType.IsAssignableFrom( valueType )
		       || Field.FieldType.IsAssignableFrom( valueType );
	}

	public bool HideInEditor => false;

	public INode CreateNode( IGraph graph )
	{
		var actionGraph = (ActionGraph)graph;
		var node = actionGraph.Graph.AddNode( Kind switch
		{
			PropertyNodeKind.Set => actionGraph.Graph.NodeLibrary.SetField,
			PropertyNodeKind.Get => actionGraph.Graph.NodeLibrary.GetField,
			_ => throw new NotImplementedException()
		} );

		node.Properties["_type"].Value = Field.TypeDescription.TargetType;
		node.Properties["_name"].Value = Field.Name;

		return new ActionNode( actionGraph, node );
	}

	public string Identifier => $"field.{Kind}/{Field.TypeDescription.FullName}/{Field.Name}";
}

public class ActionNode : INode
{
	[HideInEditor]
	public INodeType Type => new ActionNodeType( Definition );

	[HideInEditor]
	public ActionGraph Graph { get; }

	[HideInEditor]
	public Node Node { get; }

	[HideInEditor]
	public NodeDefinition Definition => Node.Definition;

	public event Action Changed;

	[HideInEditor]
	public string Identifier { get; }

	[HideInEditor]
	public string ErrorMessage => string.Join( Environment.NewLine,
		Node.GetMessages()
			.Where( x => x.IsError )
			.Select( FormatMessage ) );

	private static string FormatProperty( Node.Property property )
	{
		return property.Definition.Display.Title;
	}

	private static string FormatInput( Node.Input input )
	{
		return input.Definition.Display.Title;
	}

	private static string FormatOutput( Node.Output output )
	{
		return output.Definition.Display.Title;
	}

	private string FormatMessage( ValidationMessage message )
	{
		return message.Context switch
		{
			Link link when link.Target.Node == Node => $"{FormatInput(link.Target)}: {message.Value}",
			Node.Property property when property.Node == Node => $"{FormatProperty( property )}: {message.Value}",
			Node.Input input when input.Node == Node => $"{FormatInput(input)}: {message.Value}",
			Node.Output output when output.Node == Node => $"{FormatOutput( output )}: {message.Value}",
			_ => message.Value
		};
	}

	[HideInEditor]
	public virtual DisplayInfo DisplayInfo => Node.GetDisplayInfo();

	[HideInEditor]
	public bool CanClone => Node.Definition != EditorNodeLibrary.Input && Node.Definition != EditorNodeLibrary.Output;

	[HideInEditor]
	public bool CanRemove => Node.Definition != EditorNodeLibrary.Input && Node.Definition != EditorNodeLibrary.Output;

	[HideInEditor]
	public Color PrimaryColor
	{
		get
		{
			if ( Node.HasErrors() )
			{
				return Theme.Red;
			}

			var baseColor = Node.Kind switch
			{
				_ when Node.Definition.Identifier.StartsWith( "var." ) =>
					Color.Lerp( new Color( 0.7f, 0.7f, 0.7f ), Color.Parse( "#811EFC" )!.Value, 0.5f ),
				NodeKind.Action =>
					Color.Lerp( new Color( 0.7f, 0.7f, 0.7f ), Color.Parse( "#1997FF" )!.Value, 0.5f ),
				NodeKind.Expression when Node.Definition.Identifier.StartsWith( "const." ) =>
					Color.Lerp( new Color( 0.7f, 0.7f, 0.7f ), Color.Parse( "#1CFF39" )!.Value, 0.5f ),
				NodeKind.Expression =>
					Color.Lerp( new Color( 0.7f, 0.7f, 0.7f ), Color.Parse( "#FFF31C" )!.Value, 0.5f ),
				_ => throw new NotImplementedException()
			};

			if ( Node.GetMessages().Any( x => x is { Level: MessageLevel.Warning, Value: "Node is unreachable." } ) )
			{
				baseColor = baseColor.Desaturate( 0.75f ).Darken( 0.5f );
			}

			return baseColor;
		}
	}

	[HideInEditor]
	public Vector2 Position
	{
		get => _position.Value;
		set => _position.Value = value;
	}

	private const float ConstStringFontSize = 10f;
	private const float ConstStringMaxWidth = 240f;
	private const float ConstStringMaxHeight = 160f;

	private Vector2 ConstStringExpandSize()
	{
		Paint.SetFont( null, ConstStringFontSize );
		var size = Paint.MeasureText(
			new Rect( 0f, 0f, ConstStringMaxWidth, ConstStringMaxHeight ),
			$"\"{Node.Properties["value"].Value as string}\"",
			TextFlag.WordWrap ).Size;

		return size with { x = Math.Max( size.x - 120f, 0f ) };
	}

	[HideInEditor]
	public Vector2 ExpandSize
	{
		get
		{
			return Definition.Identifier switch
			{
				"const.string" => ConstStringExpandSize(),
				"const.sound" => default,
				"const.model" => default,
				"const.material" => default,
				"const.resource" => default,
				{ } s when s.StartsWith( "op." ) => new Vector2( -60f, 15f ),
				{ } s when s.StartsWith( "const." ) => new Vector2( -40f, 12f ),
				_ => default
			};
		}
	}

	public class PlugCollection : IEnumerable<IPlug>
	{
		public static PlugCollection Create<T, TDef>( ActionNode node, Func<IReadOnlyDictionary<string, T>> getParams )
			where T : Node.Parameter<TDef>
			where TDef : class, IParameterDefinition
		{
			return new PlugCollection(
				typeof(T) == typeof(Node.Input)
					? () => getParams().Values.Select( x => (Node.Input)(object)x )
						.SelectMany( x =>
							x.LinkArray?
								.Select( ( _, i ) => (x.Name, i) )
								.Concat( new (string, int)[] { (x.Name, x.LinkArray.Count) } ) ??
							new (string, int)[] { (x.Name, 0) } )
					: () => getParams().Keys.Select( x => (x, 0) ),
				key => new ActionPlug<T, TDef>( node, getParams()[key.Name], key.Index ) );
		}

		private readonly Func<IEnumerable<(string Name, int Index)>> _getKeys;
		private readonly Func<(string Name, int Index), IActionPlug> _createPlug;

		private readonly Dictionary<(string Name, int Index), IActionPlug> _plugs = new();

		private (string Name, int Index)[] _sortedKeys;

		private PlugCollection( Func<IEnumerable<(string Name, int Index)>> getKeys, Func<(string Name, int Index), IActionPlug> createPlug )
		{
			_getKeys = getKeys;
			_createPlug = createPlug;
		}

		public bool Update()
		{
			_sortedKeys = _getKeys().ToArray();
			var changed = false;

			foreach ( var key in _plugs.Keys.Where( x => !_sortedKeys.Contains( x ) ).ToArray() )
			{
				_plugs.Remove( key );
				changed = true;
			}

			foreach ( var key in _sortedKeys )
			{
				if ( _plugs.TryGetValue( key, out var plug ) )
				{
					if ( plug.LastType != plug.Type )
					{
						plug.LastType = plug.Type;
						changed = true;
					}
				}
				else
				{
					_plugs[key] = _createPlug( key );
					changed = true;
				}
			}

			return changed;
		}

		public IPlug this[ string name, int index ] => _plugs.TryGetValue( (name, index), out var plug ) ? plug : null;
		public IPlug this[ string name ] => _plugs.TryGetValue( (name, 0), out var plug ) ? plug : null;

		public IEnumerator<IPlug> GetEnumerator()
		{
			return (_sortedKeys ?? Enumerable.Empty<(string Name, int Index)>())
				.Select( x => this[x.Name, x.Index] )
				.GetEnumerator();
		}
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}

	[HideInEditor]
	IEnumerable<IPlug> INode.Inputs => Inputs;

	[HideInEditor]
	IEnumerable<IPlug> INode.Outputs => Outputs;

	void INode.OnPaint( Rect rect )
	{
		if ( Definition.Identifier.StartsWith( "op." ) )
		{
			PaintOperator( rect );
		}
		else if ( Definition.Identifier.StartsWith( "const." ) )
		{
			PaintConstant( rect );
		}
	}

	public void OnDoubleClick()
	{
		if ( Definition.Identifier == "graph" )
		{
			var graph = Node.Properties["graph"].Value as string;

			if ( !string.IsNullOrEmpty( graph ) )
			{
				var asset = AssetSystem.FindByPath( graph );
				asset?.OpenInEditor();
			}
		}
	}

	[HideInEditor]
	public bool HasTitleBar => !Definition.Identifier.StartsWith( "op." );

	private void PaintOperator( Rect rect )
	{
		Paint.SetPen( Theme.ControlText );
		Paint.DrawIcon( rect, Node.DisplayInfo.Icon, 50 );
	}

	private void PaintConstant( Rect rect )
	{
		rect = rect.Shrink( 3f, 30f, 3f, 3f );

		var valueProperty = Node.Properties["value"];
		var value = valueProperty.Value ?? valueProperty.Definition.Default;

		Paint.SetFont( null, 12f, 800 );

		switch ( value )
		{
			case Resource resource:
				Paint.SetPen( Color.White );
				Paint.DrawText( rect, resource.ResourceName ?? "null" );
				break;

			case Color colorVal:
				Paint.SetBrush( colorVal );
				Paint.DrawRect( rect, 3f );
				Paint.SetPen( colorVal.r + colorVal.g + colorVal.b > 1.5f ? Color.Black : Color.White );
				Paint.DrawText( rect, colorVal.Hex );
				break;

			case string str:
				Paint.SetPen( Color.White );
				Paint.SetFont( null, ConstStringFontSize );
				Paint.DrawText( new Rect(
						rect.Center - new Vector2( ConstStringMaxWidth * 0.5f, ConstStringMaxHeight * 0.5f ),
						new Vector2( ConstStringMaxWidth, ConstStringMaxHeight ) ),
					$"\"{str}\"", TextFlag.WordWrap | TextFlag.Center );
				break;

			case float floatVal:
				Paint.SetPen( Color.White );
				Paint.DrawText( rect, $"{floatVal:F1}".Length > $"{floatVal}".Length ? $"{floatVal:F1}" : $"{floatVal}" );
				break;

			default:
				Paint.SetPen( Color.White );
				Paint.DrawText( rect, value?.ToString() ?? "null" );
				break;
		}
	}

	public virtual NodeUI CreateUI( GraphView view )
	{
		return new NodeUI( view, this );
	}

	[HideInEditor]
	private readonly UserDataProperty<Vector2> _position;

	[HideInEditor]
	public PlugCollection Inputs { get; }

	[HideInEditor]
	public PlugCollection Outputs { get; }

	public ActionNode( ActionGraph graph, Node node )
	{
		Graph = graph;
		Node = node;
		Identifier = $"{node.Id}";

		_position = new UserDataProperty<Vector2>( node.UserData, nameof(Position) );

		Inputs = PlugCollection.Create<Node.Input, InputDefinition>( this, () => node.Inputs );
		Outputs = PlugCollection.Create<Node.Output, OutputDefinition>( this, () => node.Outputs );

		Update();
	}

	public void MarkDirty()
	{
		Graph.MarkDirty( this );
	}

	public void Update()
	{
		var changed = false;

		changed |= Inputs.Update();
		changed |= Outputs.Update();
		changed |= UpdateProperties();
		changed |= UpdateMessages();

		if ( changed )
		{
			Changed?.Invoke();
		}
	}

	private object _prevValue;
	private bool UpdateProperties()
	{
		if ( !Definition.Identifier.StartsWith( "const." ) )
		{
			return false;
		}

		var value = Node.Properties["value"].Value;

		if ( _prevValue == value )
		{
			return false;
		}

		_prevValue = value;
		return true;
	}

	[HideInEditor]
	private ValidationMessage[] _messages = Array.Empty<ValidationMessage>();

	private bool UpdateMessages()
	{
		var oldMessages = _messages;
		var newMessages = _messages = Node.GetMessages().ToArray();

		if ( oldMessages.Length != newMessages.Length )
		{
			return true;
		}

		for ( var i = 0; i < oldMessages.Length; i++ )
		{
			if ( !oldMessages[i].Equals( newMessages[i] ) )
			{
				return true;
			}
		}

		return false;
	}
}

public class RerouteActionNode : ActionNode
{
	public RerouteActionNode( ActionGraph graph, Node node )
		: base( graph, node )
	{
	}

	public override NodeUI CreateUI( GraphView view )
	{
		return new RerouteUI( view, this );
	}
}

public class CommentActionNode : ActionNode, ICommentNode
{
	[HideInEditor]
	private readonly UserDataProperty<int> _layer;

	[HideInEditor]
	private readonly UserDataProperty<Vector2> _size;

	[HideInEditor]
	private readonly UserDataProperty<CommentColor> _color;

	[HideInEditor]
	private readonly UserDataProperty<string> _title;

	[HideInEditor]
	private readonly UserDataProperty<string> _description;

	[HideInEditor]
	public override DisplayInfo DisplayInfo => new ()
	{
		Name = Title, Description = Description, Icon = "notes"
	};

	public CommentActionNode( ActionGraph graph, Node node )
		: base( graph, node )
	{
		_layer = new UserDataProperty<int>( Node.UserData, nameof(Layer) );
		_size = new UserDataProperty<Vector2>( Node.UserData, nameof(Size) );
		_color = new UserDataProperty<CommentColor>( Node.UserData, nameof(Color), CommentColor.Green );
		_title = new UserDataProperty<string>( Node.UserData, nameof(Title), "Unnamed" );
		_description = new UserDataProperty<string>( Node.UserData, nameof(Description), "" );
	}

	public override NodeUI CreateUI( GraphView view )
	{
		return new CommentUI( view, this );
	}

	[HideInEditor]
	public int Layer
	{
		get => _layer.Value;
		set => _layer.Value = value;
	}

	[HideInEditor]
	public Vector2 Size
	{
		get => _size.Value;
		set => _size.Value = value;
	}

	public CommentColor Color
	{
		get => _color.Value;
		set => _color.Value = value;
	}

	public string Title
	{
		get => _title.Value;
		set => _title.Value = value;
	}

	public string Description
	{
		get => _description.Value;
		set => _description.Value = value;
	}
}

public interface IActionPlug : IPlug
{
	Type LastType { get; set; }
}

public class ActionPlug<T, TDef> : IActionPlug
	where T : Node.Parameter<TDef>
	where TDef : class, IParameterDefinition
{
	public ActionNode Node { get; }
	public T Parameter { get; }
	public int Index { get; set; }

	public Type LastType { get; set; }

	INode IPlug.Node => Node;

	public string Identifier => Parameter.Name;

	public Type Type => Parameter.Type;

	public DisplayInfo DisplayInfo => new()
	{
		Name = Index == 0 && Parameter is not Node.Input { LinkArray: not null }
			? Parameter.Display.Title : $"{Parameter.Display.Title}[{Index}]",
		Description = Parameter.Display.Description
	};

	public ActionPlug( ActionNode node, T parameter, int index )
	{
		Node = node;
		Parameter = parameter;
		Index = index;

		LastType = Type;
	}

	public ValueEditor CreateEditor( NodeUI node, Plug plug )
	{
		return null;
	}

	public bool ShowLabel => Node.Definition.Identifier != "nop" && !Node.Definition.Identifier.StartsWith( "const." ) && !Node.Definition.Identifier.StartsWith( "op." );
	public bool InTitleBar => Parameter.Name == "_signal";

	public string ErrorMessage => string.Join( Environment.NewLine, Parameter.GetMessages()
		.Where( x => x.IsError )
		.Select( x => x.Value ) );

	public IPlug ConnectedOutput
	{
		get
		{
			if ( Parameter is not Node.Input input )
			{
				return null;
			}

			if ( Index is 0 && input.Link is { } link )
			{
				return Node.Graph.FindNode( link.Source.Node )?.Outputs[link.Source.Name];
			}

			if ( input.LinkArray is not { } links )
			{
				return null;
			}

			if ( Index < 0 || Index >= links.Count )
			{
				return null;
			}

			link = links[Index];

			return Node.Graph.FindNode( link.Source.Node )?.Outputs[link.Source.Name];
		}
		set
		{
			if ( Parameter is not Node.Input input )
			{
				return;
			}

			if ( value is null )
			{
				if ( !input.IsLinked )
				{
					return;
				}

				if ( Index is 0 && input.LinkArray?.Count == 1 || !input.IsArray )
				{
					input.ClearLinks();
				}
				else
				{
					input.SetLinks( (input.LinkArray ?? Array.Empty<Link>())
						.Select( ( x, i ) => i == Index ? null : x.Source )
						.Where( x => x != null )
						.ToArray() );
				}

				Node.MarkDirty();
				return;
			}

			if ( value is not ActionPlug<Node.Output, OutputDefinition> { Parameter: {} output } )
			{
				return;
			}

			if ( !input.IsArray || value.Type.IsArray && Index is 0 && input.LinkArray?.Count == 1 )
			{
				input.SetLink( output );
				Node.MarkDirty();
				return;
			}

			var links = (input.LinkArray! ?? Array.Empty<Link>())
				.Select( x => x.Source )
				.ToList();

			if ( Index == links.Count )
			{
				links.Add( output );
			}
			else
			{
				links[Index] = output;
			}

			input.SetLinks( links );
			Node.MarkDirty();
		}
	}

	public override string ToString()
	{
		return $"{Node.Identifier}.{Identifier}";
	}
}
