﻿using Sandbox.TerrainEngine;
using System;
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
			hc.Add( Value.Networked );
			hc.Add( Value.Network.IsOwner );
			hc.Add( Value.IsProxy );
			hc.Add( Value.Active );

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
		var isNetworked = Value.Networked;

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

		if ( isNetworked )
		{
			icon = "rss_feed";
			iconColor = Theme.Blue.WithAlpha( 0.8f );

			if ( Value.Network.IsOwner )
			{
				iconColor = Theme.Green.WithAlpha( 0.8f );
			}

			if ( Value.IsProxy )
			{
				iconColor = Theme.ControlText.WithAlpha( 0.6f );
			}
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
		r.Left += Paint.DrawText( r, name, TextFlag.LeftCenter ).Width;
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

		m.OpenAtCursor( false );

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

			if ( Value is not Scene )
			{
				go.Transform.Local = Transform.Zero;
			}

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
		var prefabs = AssetSystem.All.Where( x => x.AssetType.FileExtension == PrefabFile.FileExtension )
						.Where( x => x.RelativePath.StartsWith( "templates/gameobject/" ) )
						.Select( x => x.LoadResource<PrefabFile>() )
						.Where( x => x.ShowInMenu )
						.OrderByDescending( x => x.MenuPath.Count( x => x == '/' ) )
						.ThenBy( x => x.MenuPath )
						.ToArray();

		Vector3 pos = SceneEditorSession.Active.CameraPosition + SceneEditorSession.Active.CameraRotation.Forward * 300;

		// I wonder if we should be tracing and placing it on the surface?

		menu.AddOption( "Create Empty", "dataset", () =>
		{
			using var scope = SceneEditorSession.Scope();
			var go = new GameObject( true, "Object" );
			go.Transform.Local = new Transform( pos );
			then( go );
		} );

		foreach ( var entry in prefabs )
		{
			menu.AddOption( entry.MenuPath.Split( '/' ), entry.MenuIcon, () =>
			{
				using var scope = SceneEditorSession.Scope();
				var go = SceneUtility.Instantiate( entry.Scene, Transform.Zero );
				go.BreakFromPrefab();
				go.Name = entry.MenuPath.Split( '/' ).Last();
				go.Transform.Local = new Transform( pos );
				then( go );
			} );
		}

		// this isn't really going to work as a prefab because I want to save a new terrain asset?
		menu.AddOption( "3D Object/Terrain".Split( '/' ), "⛰️", () =>
		{
			/*var fd = new FileDialog( null );
			fd.Title = $"Save Terrain Data..";
			// fd.Directory = lastDirectory;
			fd.DefaultSuffix = $".terrain";
			fd.SetFindFile();
			fd.SetModeSave();
			fd.SetNameFilter( $"Terrain Data (*.terrain)" );

			if ( !fd.Execute() )
				return;

			var saveLocation = fd.SelectedFile;*/

			using var scope = SceneEditorSession.Scope();
			var go = new GameObject( true, "Terrain" );
			go.Transform.Local = new Transform( pos );

			var terrainData = new TerrainData();

			// var asset = AssetSystem.CreateResource( "terrain", saveLocation );
			// asset.SaveToDisk( terrainData );

			var terrain = go.Components.Create<Terrain>( false );
			terrain.TerrainData = terrainData;
			terrain.TerrainMaterial = Material.Load( "materials/terrain_grid.vmat" );

			terrain.Enabled = true;

			go.Components.Create<TerrainCollider>();

			then( go );
		} );
	}


}

