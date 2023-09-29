
using Editor;
using Sandbox;
using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

public partial class GameObjectNode : TreeNode<GameObject>
{
	public GameObjectNode( GameObject o ) : base ( o )
	{

	}

	public override bool HasChildren => Value.Children.Any();

	protected override void BuildChildren()
	{
		var children = Children.ToList();

		foreach ( var child in Value.Children )
		{
			var c = children.OfType<GameObjectNode>().FirstOrDefault( x => x.Value == child );
			if ( c == null )
			{
				AddItem( new GameObjectNode( child ) );
			}
			else
			{
				children.Remove( c );
			}
		}

		foreach ( var child in children )
		{
			RemoveItem( child );
		}

	}

	public override int ValueHash => HashCode.Combine( Value, Value.Children.Count, Value.Name );

	public override void OnPaint( VirtualWidget item )
	{
		var selected = item.Selected || item.Pressed;

		var fullSpanRect = item.Rect;
		fullSpanRect.Left = 0;
		fullSpanRect.Right = TreeView.Width;

		float opacity = 0.9f;

		if ( !Value.Active ) opacity *= 0.5f;

		if ( selected )
		{
			//item.PaintBackground( Color.Transparent, 3 );
			Paint.ClearPen();
			Paint.SetBrush( Theme.Blue.WithAlpha( 0.4f * opacity ) );
			Paint.DrawRect( fullSpanRect );

			Paint.SetPen( Color.White.WithAlpha( opacity ) );
		}
		else
		{
			Paint.SetPen( Theme.ControlText.WithAlpha( opacity ) );
		}

		var name = Value.Name;
		if ( string.IsNullOrWhiteSpace( name ) ) name = "Untitled GameObject";

		var r = item.Rect;
		r.Left += 4;
		 
		if ( !selected ) Paint.SetPen( Theme.Blue.WithAlpha( opacity ).Saturate( opacity - 1.0f ) );
		Paint.DrawIcon( r, "circle", 14, TextFlag.LeftCenter );
		r.Left += 22;

		Paint.SetPen( selected ? Theme.White.WithAlpha( opacity ) : Theme.ControlText.WithAlpha( opacity ) );
		Paint.SetDefaultFont( 9 );
		Paint.DrawText( r, name, TextFlag.LeftCenter );
	}

	public override bool OnDragStart()
	{
		var drag = new Drag( TreeView );
		//drag.Data.Text = Json.Serialize( Value.Serialize() );
		drag.Data.Object = Value;
		drag.Execute();

		return true;
	}

	public override void OnDragHover( Widget.DragEvent ev )
	{
		if ( ev.Data.Object is GameObject go )
		{
			ev.Action = DropAction.Move;
			return;
		}

		base.OnDragHover( ev );
	}

	public override void OnDrop( Widget.DragEvent ev )
	{
		if ( ev.Data.Object is GameObject go )
		{
			ev.Action = DropAction.Move;
			go.Parent = Value;
			return;
		}

		base.OnDrop( ev );
	}

	public override bool OnContextMenu()
	{
		var m = new Menu();

		m.AddOption( "Cut", action: Cut );
		m.AddOption( "Copy", action: Copy );
		m.AddOption( "Paste", action: Paste );
		m.AddOption( "Paste As Child", action: PasteAsChild );
		m.AddSeparator();
		//m.AddOption( "rename", action: Delete );
		//m.AddOption( "duplicate", action: Delete );
		m.AddOption( "Delete", action: Delete );

		m.AddSeparator();

		CreateObjectMenu( m, go =>
		{
			go.Parent = Value;
			TreeView.Open( this );
			TreeView.SelectItem( go );
		} );

		// cut
		// copy
		// paste 
		// paste as child
		// --
		// rename
		// duplicate
		// delete

		m.AddSeparator();
		m.AddOption( "Properties..", action: OpenPropertyWindow );

		m.OpenAtCursor();

		return true;
	}

	void Cut()
	{
		Copy();
		Delete();
	}

	void Copy()
	{
		var json = Value.Serialize();
		EditorUtility.Clipboard.Copy( json.ToString() );
	}

	void Paste()
	{
		var text = EditorUtility.Clipboard.Paste();
		if ( JsonNode.Parse( text ) is JsonObject jso )
		{
			var go = Value.Scene.CreateObject();
			go.Deserialize( jso );
			go.Parent = Value.Parent;

			TreeView.SelectItem( go );
		}
	}

	void PasteAsChild()
	{
		var text = EditorUtility.Clipboard.Paste();
		if ( JsonNode.Parse( text ) is JsonObject jso )
		{
			var go = new GameObject();
			go.Deserialize( jso );
			go.Parent = Value;

			TreeView.Open( this );
			TreeView.SelectItem( go );
		}
	}

	void Delete()
	{
		Value.Destroy();
	}

	void OpenPropertyWindow()
	{

	}

	public static void CreateObjectMenu( Menu menu, Action<GameObject> then )
	{
		menu.AddOption( "Create Empty", "category", () =>
		{
			var go = new GameObject();
			go.Name = "Object";
			then( go );
		} );

		// 3d obj
		{
			var submenu = menu.AddMenu( "3D Object" );

			submenu.AddOption( "Cube", "category", () =>
			{
				var go = new GameObject();
				go.Name = "Cube";

				var model = go.AddComponent<ModelComponent>();
				model.Model = Model.Load( "models/dev/box.vmdl" );

				then( go );
			} );

			submenu.AddOption( "Sphere", "category", () =>
			{
				var go = new GameObject();
				go.Name = "Sphere";

				var model = go.AddComponent<ModelComponent>();
				model.Model = Model.Load( "models/dev/sphere.vmdl" );

				then( go );
			} );


			submenu.AddOption( "Plane", "category", () =>
			{
				var go = new GameObject();
				go.Name = "Plane";

				var model = go.AddComponent<ModelComponent>();
				model.Model = Model.Load( "models/dev/plane.vmdl" );

				then( go );
			} );
		}

		{
			menu.AddOption( "Camera", "category", () =>
			{
				var go = new GameObject();
				go.Name = "Camera";

				var cam = go.AddComponent<CameraComponent>();

				then( go );
			} );

		}
	}
}

