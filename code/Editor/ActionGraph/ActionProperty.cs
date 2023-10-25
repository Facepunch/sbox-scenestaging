using System;
using System.Text.Json;
using System.Xml.Linq;
using Facepunch.ActionGraphs;
using Sandbox;

namespace Editor.ActionGraph;

[CanEdit( "action" )]
public sealed class ActionProperty<T> : Button
	where T : Delegate
{
	public T Value { get; set; }

	public ActionProperty( Widget parent ) : base( parent )
	{
		Icon = "account_tree";
		Text = "Open Editor";
	}

	protected override void OnClicked()
	{
		Value ??= Facepunch.ActionGraphs.ActionGraph.Create<T>( EditorNodeLibrary );

		// TODO: Would be good to use the containing property name if possible
		var name = typeof(T).Name;

		MainWindow.Open( (Facepunch.ActionGraphs.ActionGraph)Value, name );
	}
}

[CustomEditor(typeof(Delegate))]
public sealed class ActionControlWidget : ControlWidget
{
	public Button Button { get; }

	private MainWindow _openWindow;

	public ActionControlWidget( SerializedProperty property ) : base( property )
	{
		Button = new Button( "Open In Editor", "account_tree", this );
		Button.Clicked += Button_Clicked;
	}

	private void Button_Clicked()
	{
		var action = SerializedProperty.GetValue<Delegate>();
		Facepunch.ActionGraphs.ActionGraph graph;

		try
		{
			graph = action == null ? null : (Facepunch.ActionGraphs.ActionGraph) action;
		}
		catch ( InvalidCastException )
		{
			graph = null;
		}

		graph ??= Facepunch.ActionGraphs.ActionGraph.Create( EditorNodeLibrary, SerializedProperty.PropertyType );

		var name = SerializedProperty.DisplayName;
		var window = MainWindow.Open( graph, name );

		if ( _openWindow == window )
		{
			return;
		}

		_openWindow = window;

		window.Saved += () =>
		{
			SerializedProperty.SetValue( window.ActionGraph.AsDelegate( SerializedProperty.PropertyType ) );
			SerializedProperty.Parent.NoteChanged( SerializedProperty );
		};
	}

	protected override Vector2 SizeHint()
	{
		Button.AdjustSize();
		return new Vector2( 128f, Button.Height );
	}

	protected override void DoLayout()
	{
		base.DoLayout();

		Button.Position = 0f;
		Button.Size = Size;
	}

	protected override void OnValueChanged()
	{
		base.OnValueChanged();

	}
}
