using System;
using static Editor.BaseItemWidget;

public partial class GameObjectNode : TreeNode<GameObject>
{
	public GameObjectNode( GameObject o ) : base( o )
	{
		Height = 17;
	}

	public override bool HasChildren => Value.Children.Any();

	protected override void BuildChildren()
	{
		SetChildren( Value.Children, x => new GameObjectNode( x ) );
	}

	public override int ValueHash
	{
		get
		{
			HashCode hc = new HashCode();
			hc.Add( Value.Name );

			foreach ( var val in Value.Children )
			{
				hc.Add( val );
			}

			return hc.ToHashCode();
		}
	}

	public override void OnPaint( VirtualWidget item )
	{
		var selected = item.Selected || item.Pressed || item.Dragging;

		var fullSpanRect = item.Rect;
		fullSpanRect.Left = 0;
		fullSpanRect.Right = TreeView.Width;

		float opacity = 0.9f;

		if ( !Value.Active ) opacity *= 0.5f;

		//
		// If there's a drag and drop happening, fade out nodes that aren't possible
		//
		if ( TreeView.IsBeingDroppedOn && (TreeView.CurrentItemDragEvent.Data.Object is not GameObject go || Value.IsAncestor( go )) )
		{
			opacity *= 0.23f;
		}

		if ( item.Dropping )
		{
			Paint.ClearPen();
			Paint.SetBrush( Theme.Blue.WithAlpha( 0.2f ) );

			if ( TreeView.CurrentItemDragEvent.DropEdge.HasFlag( ItemEdge.Top ) )
			{
				var droprect = item.Rect;
				droprect.Top -= 1;
				droprect.Height = 2;
				Paint.DrawRect( droprect, 2 );
			}
			else if ( TreeView.CurrentItemDragEvent.DropEdge.HasFlag( ItemEdge.Bottom ) )
			{
				var droprect = item.Rect;
				droprect.Top = droprect.Bottom - 1;
				droprect.Height = 2;
				Paint.DrawRect( droprect, 2 );
			}
			else
			{
				Paint.DrawRect( item.Rect, 2 );
			}
		}

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
		Paint.SetDefaultFont();
		Paint.DrawText( r, name, TextFlag.LeftCenter );
	}

	public override bool OnDragStart()
	{
		var drag = new Drag( TreeView );
		drag.Data.Object = Value;
		drag.Execute();

		return true;
	}

	public override DropAction OnDragDrop( ItemDragEvent e )
	{
		if ( e.Data.Object is GameObject go )
		{
			// can't parent to an ancesor
			if ( go == Value || Value.IsAncestor( go ) )
				return DropAction.Ignore;

			if ( e.IsDrop )
			{
				if ( e.DropEdge.HasFlag( ItemEdge.Top ) )
				{
					Value.AddSibling( go, true );
				}
				else if ( e.DropEdge.HasFlag( ItemEdge.Bottom ) )
				{
					Value.AddSibling( go, false );
				}
				else
				{
					go.SetParent( Value, true );
				}
			}

			return DropAction.Move;
		}

		return DropAction.Ignore;
	}

	public override bool OnContextMenu()
	{
		var m = new Menu();

		AddGameObjectMenuItems( m );

		m.OpenAtCursor();

		return true;
	}

	protected void AddGameObjectMenuItems( Menu m )
	{
		m.AddOption( "Cut", action: SceneEditorMenus.Cut );
		m.AddOption( "Copy", action: SceneEditorMenus.Copy );
		m.AddOption( "Paste", action: SceneEditorMenus.Paste );
		m.AddOption( "Paste As Child", action: SceneEditorMenus.PasteAsChild );
		m.AddSeparator();
		//m.AddOption( "rename", action: Delete );
		m.AddOption( "Duplicate", action: SceneEditorMenus.Duplicate );
		m.AddOption( "Delete", action: SceneEditorMenus.Delete );

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
		m.AddOption( "Convert To Prefab..", action: ConvertToPrefab );
		m.AddOption( "Properties..", action: OpenPropertyWindow );
	}

	void OpenPropertyWindow()
	{
		Log.Info( "TODO: OpenPropertyWindow" );
	}

	void ConvertToPrefab()
	{
		var saveLocation = "";

		var a = Value.GetAsPrefab();

		var lastDirectory = Cookie.GetString( "LastSavePrefabLocation", "" );

		var fd = new FileDialog( null );
		fd.Title = $"Save Scene As..";
		fd.Directory = lastDirectory;
		fd.DefaultSuffix = $".{PrefabFile.FileExtension}";
		fd.SelectFile( saveLocation );
		fd.SetFindFile();
		fd.SetModeSave();
		fd.SetNameFilter( $"Prefab File (*.{PrefabFile.FileExtension})" );

		if ( !fd.Execute() )
			return;

		saveLocation = fd.SelectedFile;

		var sceneAsset = AssetSystem.CreateResource( PrefabFile.FileExtension, saveLocation );
		sceneAsset.SaveToDisk( a );
	}

	public override void OnActivated()
	{
		SceneEditorMenus.Frame();
	}

	public static void CreateObjectMenu( Menu menu, Action<GameObject> then )
	{
		menu.AddOption( "Create Empty", "category", () =>
		{
			using var scope = EditorScene.Active.Push();
			var go = GameObject.Create( true, "Object" );
			then( go );
		} );

		// 3d obj
		{
			var submenu = menu.AddMenu( "3D Object" );

			submenu.AddOption( "Cube", "category", () =>
			{
				using var scope = EditorScene.Active.Push();
				var go = GameObject.Create();
				go.Name = "Cube";

				var model = go.AddComponent<ModelComponent>();
				model.Model = Model.Load( "models/dev/box.vmdl" );

				then( go );
			} );

			submenu.AddOption( "Sphere", "category", () =>
			{
				using var scope = EditorScene.Active.Push();
				var go = GameObject.Create();
				go.Name = "Sphere";

				var model = go.AddComponent<ModelComponent>();
				model.Model = Model.Load( "models/dev/sphere.vmdl" );

				then( go );
			} );


			submenu.AddOption( "Plane", "category", () =>
			{
				using var scope = EditorScene.Active.Push();
				var go = GameObject.Create();
				go.Name = "Plane";

				var model = go.AddComponent<ModelComponent>();
				model.Model = Model.Load( "models/dev/plane.vmdl" );

				then( go );
			} );
		}

		// 3d obj
		{
			var submenu = menu.AddMenu( "Light" );

			submenu.AddOption( "Directional Light", "category", () =>
			{
				using var scope = EditorScene.Active.Push();
				var go = GameObject.Create();
				go.Name = "Directional Light";
				go.Transform.Rotation = Rotation.LookAt( Vector3.Down + Vector3.Right * 0.25f );

				var model = go.AddComponent<DirectionalLightComponent>();

				then( go );
			} );

			submenu.AddOption( "Point Light", "category", () =>
			{
				using var scope = EditorScene.Active.Push();
				var go = GameObject.Create();
				go.Name = "Point Light";

				var model = go.AddComponent<PointLightComponent>();

				then( go );
			} );


			submenu.AddOption( "Spot Light", "category", () =>
			{
				using var scope = EditorScene.Active.Push();
				var go = GameObject.Create();
				go.Name = "Spot Light";

				var model = go.AddComponent<SpotLightComponent>();

				then( go );
			} );
		}

		{
			menu.AddOption( "Camera", "category", () =>
			{
				using var scope = EditorScene.Active.Push();
				var go = GameObject.Create();
				go.Name = "Camera";

				var cam = go.AddComponent<CameraComponent>();

				then( go );
			} );

		}
	}
}

