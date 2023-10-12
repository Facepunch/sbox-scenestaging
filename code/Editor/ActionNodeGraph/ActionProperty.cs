using System;
using System.Text.Json;
using System.Xml.Linq;
using Facepunch.ActionJigs;
using Sandbox;

namespace Editor.ActionJigs;

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
		Value ??= ActionJig.Create<T>( EditorNodeLibrary );

		// TODO: Would be good to use the containing property name if possible
		var name = typeof(T).Name;

		MainWindow.Open( (ActionJig)Value, name );
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
		ActionJig jig;

		try
		{
			jig = action == null ? null : (ActionJig) action;
		}
		catch ( InvalidCastException )
		{
			jig = null;
		}

		jig ??= ActionJig.Create( EditorNodeLibrary, SerializedProperty.PropertyType );

		var name = SerializedProperty.DisplayName;
		var window = MainWindow.Open( jig, name );

		if ( _openWindow == window )
		{
			return;
		}

		_openWindow = window;

		window.Saved += () =>
		{
			SerializedProperty.SetValue( window.ActionJig.AsDelegate( SerializedProperty.PropertyType ) );
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
