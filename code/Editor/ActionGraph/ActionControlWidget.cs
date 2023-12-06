using System;

namespace Editor.ActionGraphs;

[CustomEditor( typeof( Delegate ) )]
public sealed class ActionControlWidget : ControlWidget
{
	private MainWindow _openWindow;

	public ActionControlWidget( SerializedProperty property ) : base( property )
	{
		Cursor = CursorShape.Finger;
	}

	Facepunch.ActionGraphs.ActionGraph Graph
	{
		get
		{
			try
			{
				var action = SerializedProperty.GetValue<Delegate>();
				if ( action is null ) return null;
				return (Facepunch.ActionGraphs.ActionGraph)action;
			}
			catch ( InvalidCastException )
			{
				return null;
			}
		}
	}

	private void OpenEditor()
	{
		Facepunch.ActionGraphs.ActionGraph graph = Graph;

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

	void Clear()
	{
		SerializedProperty.SetValue<object>( null );
	}

	protected override void OnMousePress( MouseEvent e )
	{
		if ( e.RightMouseButton )
		{
			var menu = new Menu( this );

			menu.AddOption( "Clear", "clear", Clear );

			menu.OpenAtCursor();
		}

		if ( e.LeftMouseButton )
		{
			OpenEditor();
		}
	}

	protected override void PaintOver()
	{
		var graph = Graph;
		var rect = LocalRect.Shrink( 8, 0 );
		var alpha = Paint.HasMouseOver ? 0.7f : 0.5f;

		// icon
		{
			Paint.SetPen( graph is null ? Theme.Grey.WithAlphaMultiplied( alpha ) : Theme.Green.WithAlphaMultiplied( alpha ) );
			var r = Paint.DrawIcon( rect, graph?.Icon ?? "account_tree", 17, TextFlag.LeftCenter );
			rect.Left += r.Width + 8;
		}

		if ( graph is null )
		{
			Paint.SetPen( Theme.Grey.WithAlphaMultiplied( alpha ) );
			Paint.DrawText( rect, "Empty Action", TextFlag.LeftCenter );
		}
		else
		{
			var title = graph.Title;
			var description = graph.Description;
			if ( string.IsNullOrWhiteSpace( title ) ) title = "Action";

			Paint.SetPen( Theme.White.WithAlphaMultiplied( alpha ) );
			var r = Paint.DrawText( rect, title, TextFlag.LeftCenter );
			rect.Left += r.Width + 4;

			if ( !string.IsNullOrWhiteSpace( description ) )
			{
				Paint.SetDefaultFont( 7 );
				Paint.SetPen( Theme.White.WithAlphaMultiplied( alpha * 0.5f ) );
				Paint.DrawText( rect, description, TextFlag.LeftCenter );
			}
		}
	}
}
