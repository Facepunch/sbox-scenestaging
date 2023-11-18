﻿using System;
using System.Data;
using static Editor.BaseItemWidget;

public partial class GameObjectNode : TreeNode<GameObject>
{
	public GameObjectNode( GameObject o ) : base( o )
	{
		Height = 17;
	}

	public override string Name
	{
		get => Value.Name;
		set => Value.Name = value;
	}

	public override bool CanEdit => true;

	public override bool HasChildren
	{
		get
		{
			// hide children of prefabs
			if ( Value.IsPrefabInstance ) return false;

			return Value.Children.Any();
		}
	}

	protected override void BuildChildren() => SetChildren( Value.Children, x => new GameObjectNode( x ) );
	protected override bool HasDescendant( object obj ) => obj is GameObject go && Value.IsDescendant( go );

	public override int ValueHash
	{
		get
		{
			HashCode hc = new HashCode();
			hc.Add( Value.Name );
			hc.Add( Value.IsPrefabInstance );
			hc.Add( Value.Flags );

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
		var isBone = Value.Flags.HasFlag( GameObjectFlags.Bone );
		var isAttachment = Value.Flags.HasFlag( GameObjectFlags.Attachment );

		var fullSpanRect = item.Rect;
		fullSpanRect.Left = 0;
		fullSpanRect.Right = TreeView.Width;

		float opacity = 0.9f;

		if ( !Value.Active ) opacity *= 0.5f;

		Color pen = Theme.ControlText;
		string icon = "layers";
		Color iconColor = Theme.ControlText.WithAlpha( 0.6f );

		if ( Value.IsPrefabInstance )
		{
			pen = Theme.Blue;
			icon = "dataset";
			iconColor = Theme.Blue;

			if ( !Value.IsPrefabInstanceRoot )
			{
				icon = "dataset_linked";
				iconColor = iconColor.WithAlpha( 0.5f );
				pen = pen.WithAlpha( 0.5f );
			}
		}

		if ( isBone )
		{
			icon = "polyline";
			iconColor = Theme.Pink.WithAlpha( 0.8f );
		}

		//
		// If there's a drag and drop happening, fade out nodes that aren't possible
		//
		if ( TreeView.IsBeingDroppedOn )
		{
			if ( TreeView.CurrentItemDragEvent.Data.Object is GameObject[] gos && gos.Any( go => Value.IsAncestor( go ) ) )
			{
				opacity *= 0.23f;
			}
			else if ( TreeView.CurrentItemDragEvent.Data.Object is GameObject go && Value.IsAncestor( go ) )
			{
				opacity *= 0.23f;
			}
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
			Paint.SetBrush( Theme.Blue.WithAlpha( 0.1f * opacity ) );
			Paint.DrawRect( fullSpanRect );
		}

		var name = Value.Name;
		if ( string.IsNullOrWhiteSpace( name ) ) name = "Untitled GameObject";

		var r = item.Rect;
		r.Left += 4;

		Paint.SetPen( iconColor.WithAlphaMultiplied( opacity ) );
		Paint.DrawIcon( r, icon, 14, TextFlag.LeftCenter );
		r.Left += 22;

		Paint.SetPen( pen.WithAlphaMultiplied( opacity ) );
		Paint.SetDefaultFont();
		Paint.DrawText( r, name, TextFlag.LeftCenter );
	}

	public override bool OnDragStart()
	{
		var drag = new Drag( TreeView );
		
		if ( TreeView.IsSelected( Value ) )
		{
			// If we're selected then use all selected items in the tree.
			drag.Data.Object = TreeView.SelectedItems.OfType<GameObject>().ToArray();
		}
		else
		{
			// Otherwise let's just drag this one.
			drag.Data.Object = new[] { Value };
		}
		
		drag.Execute();

		return true;
	}

	public override DropAction OnDragDrop( ItemDragEvent e )
	{
		using var scope = Value.Scene.Push();

		if ( e.Data.Object is GameObject[] gos )
		{
			if ( gos.Any( go => go == Value || Value.IsAncestor( go ) ) )
			{
				return DropAction.Ignore;
			}
			
			foreach ( var go in gos )
			{
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
			}

			return DropAction.Move;
		}

		var asset = AssetSystem.FindByPath( e.Data.FileOrFolder );
		if ( asset is not null && asset.AssetType.FileExtension == "object" )
		{
			var pf = asset.LoadResource<PrefabFile>();
			if ( pf is null ) return DropAction.Ignore;

			if ( e.IsDrop )
			{
				var instantiated = SceneUtility.Instantiate( pf.Scene );

				if ( e.DropEdge.HasFlag( ItemEdge.Top ) )
				{
					Value.AddSibling( instantiated, true );
				}
				else if ( e.DropEdge.HasFlag( ItemEdge.Bottom ) )
				{
					Value.AddSibling( instantiated, false );
				}
				else
				{
					instantiated.SetParent( Value, true );
				}

				SceneEditorSession.Active.Selection.Set( instantiated );
			}
			
			return DropAction.Move;
		}

		return DropAction.Ignore;
	}

	public override bool OnContextMenu()
	{
		var m = new Menu( TreeView );

		AddGameObjectMenuItems( m );

		m.OpenAtCursor( true );

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

		if ( Value.IsPrefabInstanceRoot )
		{
			m.AddOption( "Unlink From Prefab", action: () => { Value.BreakFromPrefab(); } );
		}
		else
		{
			m.AddOption( "Convert To Prefab..", action: ConvertToPrefab );
		}

		
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

		Value.SetPrefabSource( sceneAsset.Path );
	}

	public override void OnActivated()
	{
		SceneEditorMenus.Frame();
	}

	public static void CreateObjectMenu( Menu menu, Action<GameObject> then )
	{
		menu.AddOption( "Create Empty", "dataset", () =>
		{
			using var scope = SceneEditorSession.Scope();
			var go = new GameObject( true, "Object" );
			then( go );
		} );

		// 3d obj
		{
			var submenu = menu.AddMenu( "3D Object", "dataset" );

			submenu.AddOption( "Cube", "category", () =>
			{
				using var scope = SceneEditorSession.Scope();
				var go = new GameObject();
				go.Name = "Cube";

				var model = go.AddComponent<ModelComponent>();
				model.Model = Model.Load( "models/dev/box.vmdl" );

				then( go );
			} );

			submenu.AddOption( "Sphere", "category", () =>
			{
				using var scope = SceneEditorSession.Scope();
				var go = new GameObject();
				go.Name = "Sphere";

				var model = go.AddComponent<ModelComponent>();
				model.Model = Model.Load( "models/dev/sphere.vmdl" );

				then( go );
			} );


			submenu.AddOption( "Plane", "category", () =>
			{
				using var scope = SceneEditorSession.Scope();
				var go = new GameObject();
				go.Name = "Plane";

				var model = go.AddComponent<ModelComponent>();
				model.Model = Model.Load( "models/dev/plane.vmdl" );

				then( go );
			} );
		}

		// light
		{
			var submenu = menu.AddMenu( "Light", "light_mode" );

			submenu.AddOption( "Directional Light", "category", () =>
			{
				using var scope = SceneEditorSession.Scope();
				var go = new GameObject();
				go.Name = "Directional Light";
				go.Transform.Rotation = Rotation.LookAt( Vector3.Down + Vector3.Right * 0.25f );
				go.AddComponent<DirectionalLightComponent>();

				then( go );
			} );

			submenu.AddOption( "Point Light", "category", () =>
			{
				using var scope = SceneEditorSession.Scope();
				var go = new GameObject();
				go.Name = "Point Light";
				go.AddComponent<PointLightComponent>();

				then( go );
			} );


			submenu.AddOption( "Spot Light", "category", () =>
			{
				using var scope = SceneEditorSession.Scope();
				var go = new GameObject();
				go.Name = "Spot Light";
				go.AddComponent<SpotLightComponent>();

				then( go );
			} );

			submenu.AddSeparator();

			submenu.AddOption( "2D SkyBox", "category", () =>
			{
				using var scope = SceneEditorSession.Scope();
				var go = new GameObject();
				go.Name = "SkyBox";

				go.AddComponent<SkyBox2D>();

				then( go );
			} );
		}

		// UI
		{
			var submenu = menu.AddMenu( "UI", "desktop_windows" );

			submenu.AddOption( "World UI", "panorama_horizontal", () =>
			{
				using var scope = SceneEditorSession.Scope();
				var go = new GameObject();
				go.Name = "World UI";
				go.AddComponent<WorldPanel>();
				then( go );
			} );

			submenu.AddOption( "Screen UI", "desktop_windows", () =>
			{
				using var scope = SceneEditorSession.Scope();
				var go = new GameObject();
				go.Name = "Screen UI";
				go.AddComponent<ScreenPanel>();

				then( go );
			} );
		}

		// Camera
		{
			var submenu = menu.AddMenu("Camera", "desktop_windows");

			submenu.AddOption("Camera", "videocam", () =>
			{
				using var scope = SceneEditorSession.Scope();
				var go = new GameObject();
				go.Name = "Camera";

				var cam = go.AddComponent<CameraComponent>();

				then(go);
			});

			submenu.AddOption("Spring Arm", "start", () =>
			{
				using var scope = SceneEditorSession.Scope();

				var parent = new GameObject();
				parent.Name = "Spring Arm";

				parent.AddComponent<SpringArmComponent>();

				var camera = new GameObject();
				camera.Parent = parent;
				camera.Name = "Camera";
				camera.AddComponent<CameraComponent>();
                
				then(parent);
			});
		}
	}
}

