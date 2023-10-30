using Editor.NodeEditor;
using Facepunch.ActionGraphs;
using System;

namespace Editor.ActionGraph;


[CustomEditor( typeof(Variable) )]
internal class VariableControlWidget : ControlWidget
{
	public Node Node =>
		(SerializedProperty as SerializedNodeParameter<Node.Property, PropertyDefinition>)
		?.Target.Node;

	public override bool IsControlButton => true;
	public override bool IsControlHovered => base.IsControlHovered || _menu.IsValid();

	Menu _menu;

	public VariableControlWidget( SerializedProperty property )
		: base( property )
	{
		Cursor = CursorShape.Finger;

		Layout = Layout.Row();
		Layout.Spacing = 2;
	}

	protected override void PaintControl()
	{
		var value = SerializedProperty.GetValue<Variable>();

		var color = IsControlHovered ? Theme.Blue : Theme.ControlText;
		var rect = LocalRect;

		rect = rect.Shrink( 8, 0 );

		Paint.SetPen( color );
		Paint.DrawText( rect, value is null ? "None" : $"{value.Name} ({GraphView.FormatTypeName( value.Type )})", TextFlag.LeftCenter );

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

	void OpenMenu()
	{
		var variables = Node.ActionGraph.Variables;

		_menu = new Menu();
		_menu.DeleteOnClose = true;

		var w = new Widget( null );
		w.Layout = Layout.Row();
		w.Layout.Margin = 6;
		w.Layout.Spacing = 4;

		var filter = new GraphView.Filter( w );
		filter.TextChanged += ( s ) => PopulateMenu( _menu, variables, s );
		filter.PlaceholderText = "Variable Name..";

		w.Layout.Add( new Label( "Name:", w ) );
		w.Layout.Add( filter );

		_menu.AddWidget( w );

		filter.Focus();

		_menu.AboutToShow += () => PopulateMenu( _menu, variables );

		_menu.OpenAtCursor( true );
		_menu.MinimumWidth = ScreenRect.Width;
	}

	private Type GetNewVariableType( Node node )
	{
		switch ( node.Definition.Identifier )
		{
			case "var.set":
				return node.Inputs["value"].SourceType ?? typeof(object);

			case "var.get":
				var type = node.Outputs.Values.Single().Links.FirstOrDefault()?.Target.Type;
				return type?.ContainsGenericParameters ?? true ? typeof(object) : type;

			default:
				return typeof(object);
		}
	}

	private void PopulateMenu( Menu menu, IEnumerable<Variable> variables, string filter = null )
	{
		menu.RemoveMenus();
		menu.RemoveOptions();

		const int maxFiltered = 10;

		var useFilter = !string.IsNullOrEmpty( filter );
		var truncated = 0;

		if ( useFilter )
		{
			var filtered = variables
				.Where( x => x.Name.Contains( filter, StringComparison.OrdinalIgnoreCase ) )
				.ToArray();

			if ( filtered.Length > maxFiltered + 1 )
			{
				truncated = filtered.Length - maxFiltered;
				variables = filtered.Take( maxFiltered );
			}
			else
			{
				variables = filtered;
			}
		}

		variables = variables
			.OrderBy( x => x.Name );

		foreach ( var variable in variables )
		{
			var option = menu.AddOption( $"{variable.Name} ({GraphView.FormatTypeName( variable.Type )})" );
			option.Triggered += () => SerializedProperty.SetValue( variable );
		}

		if ( truncated > 0 )
		{
			menu.AddOption( $"...and {truncated} more" );
		}

		if ( useFilter && filter.Length > 0 && !variables.Any( x => x.Name == filter ) )
		{
			menu.AddSeparator();
			
			menu.AddOption( $"Add \"{filter}\"", "add", shortcut: "Ctrl+Enter", action: () =>
			{
				var node = Node;
				var type = GetNewVariableType( node );
				var graph = node.ActionGraph;

				var variable = graph.AddVariable( filter, type );

				SerializedProperty.SetValue( variable );
			} );
		}

		menu.AdjustSize();
		menu.Update();
	}
}
