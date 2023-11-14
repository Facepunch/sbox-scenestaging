using Sandbox;
using System.Threading;
using System.Threading.Tasks;

[Dock( "Editor", "Scene", "grid_4x4" )]
public partial class SceneViewWidget : Widget
{
	public static SceneViewWidget Current { get; private set; }

	NativeRenderingWidget Renderer;
	public SceneCamera Camera;
	SceneViewToolbar SceneToolbar;

	public SceneViewWidget( Widget parent ) : base( parent )
	{
		Camera = new SceneCamera();
		Camera.ClearFlags = ClearFlags.Color | ClearFlags.Depth | ClearFlags.Stencil;

		AcceptDrops = true;

		Renderer = new NativeRenderingWidget( this );
		Renderer.Size = 200;
		Renderer.Camera = Camera;

		Layout = Layout.Column();
		SceneToolbar = Layout.Add( new SceneViewToolbar( this ) );
		Layout.Add( Renderer );

		Camera.Worlds.Add( EditorScene.GizmoInstance.World );
	}

	int selectionHash = 0;

	Vector3? cameraTargetPosition;
	Vector3 cameraVelocity;

	[EditorEvent.Frame]
	public void Frame()
	{
		var session = SceneEditorSession.Active;
		if ( session is null ) return;

		// Update inspector with current selection, if changed
		if ( selectionHash != session.Selection.GetHashCode() )
		{
			// todo - multiselect
			EditorUtility.InspectorObject = session.Selection.LastOrDefault();
			selectionHash = session.Selection.GetHashCode();
		}

		if ( !Visible )
			return;

		Current = this;

		// Lets default to the settings from the camera
		var camera = session.Scene.FindAllComponents<CameraComponent>( false ).FirstOrDefault();
		if ( camera is not null )
		{
			camera.UpdateCamera( Camera );
		}

		session.RestoreCamera( Camera );

		EditorScene.GizmoInstance.Selection = session.Selection;

		

		Camera.World = session.Scene.SceneWorld;
		Camera.Worlds.Add( EditorScene.GizmoInstance.World );
		Camera.ClearFlags = ClearFlags.Color | ClearFlags.Depth | ClearFlags.Stencil;
		Camera.ZNear = EditorScene.GizmoInstance.Settings.CameraZNear;
		Camera.ZFar = EditorScene.GizmoInstance.Settings.CameraZFar;
		Camera.FieldOfView = EditorScene.GizmoInstance.Settings.CameraFieldOfView;
		Camera.EnableUserInterface = false;
		Camera.EnablePostProcessing = false;
		Camera.Ortho = false;

		if ( EditorScene.GizmoInstance.GetValue<bool>( "unlit" ) )
		{
			Camera.EnableDirectLighting = false;
			Camera.EnableIndirectLighting = false;
			Camera.AmbientLightColor = Color.White;
			Camera.Bloom.Enabled = false;
			Camera.Tonemap.Enabled = false;
		}
		else
		{
			Camera.EnableDirectLighting = true;
			Camera.EnableIndirectLighting = true;
			Camera.AmbientLightColor = Color.Black;
		}

		SceneToolbar.SceneInstance = EditorScene.GizmoInstance;

		if ( cameraTargetPosition is not null )
		{
			var pos = Vector3.SmoothDamp( Camera.Position, cameraTargetPosition.Value, ref cameraVelocity, 0.3f, RealTime.Delta );
			Camera.Position = pos;

			if ( cameraTargetPosition.Value.Distance( pos ) < 0.1f )
			{
				cameraTargetPosition = null;
			}
		}

		if ( EditorScene.GizmoInstance.FirstPersonCamera( Camera, Renderer ) )
		{
			cameraTargetPosition = null;
		}

		EditorScene.GizmoInstance.UpdateInputs( Camera, Renderer );

		using ( EditorScene.GizmoInstance.Push() )
		{
			Cursor = Gizmo.HasHovered ? CursorShape.Finger : CursorShape.Arrow;

			session.Scene.EditorTick();

			if ( Gizmo.HasClicked && Gizmo.HasHovered )
			{

			}

			if ( Gizmo.HasClicked && !Gizmo.HasHovered )
			{
				Gizmo.Select();
			}
		}

		session.UpdateState( Camera );
	}

	[Event( "scene.frame" )]
	public void FrameOn( BBox target )
	{
		var distance = MathX.SphereCameraDistance( target.Size.Length, Camera.FieldOfView ) * 1.0f;
		var targetPos = target.Center + distance * Camera.Rotation.Backward;

		if ( Camera.Position.Distance( targetPos ) < 1.0f )
		{
			cameraTargetPosition = target.Center + distance * 2.0f * Camera.Rotation.Backward;
		}
		else
		{
			cameraTargetPosition = targetPos;
		}
	}

	GameObject DragObject;
	float DragOffset;
	Task DragInstallTask;
	CancellationTokenSource DragCancelSource;

	public override void OnDragDrop( DragEvent ev )
	{
		base.OnDragDrop( ev );

		using var sceneScope = SceneEditorSession.Scope();

		if ( DragObject is not null )
		{
			EditorScene.Selection.Clear();
			EditorScene.Selection.Add( DragObject );

			DragObject.Flags = GameObjectFlags.None;
			DragObject.Tags.Remove( "isdragdrop" );
			DragObject = null;
		}

		DragCancelSource?.Cancel();
		DragCancelSource = null;
	}

	public override void OnDragHover( DragEvent ev )
	{
		base.OnDragHover( ev );

		ev.Action = DropAction.Copy;

		using var sceneScope = SceneEditorSession.Scope();

		if ( DragObject is not null )
		{
			DragObject.Enabled = false;
		}

		var tr = SceneEditorSession.Active.Scene.PhysicsWorld.Trace
						.WithoutTags( "isdragdrop", "trigger" )
						.Ray( Camera.GetRay( ev.LocalPosition - Renderer.Position, Renderer.Size ), 2048 )
						.Run();

		var rot = Rotation.LookAt( tr.Normal, Vector3.Up ) * Rotation.From( 90, 0, 0 );

		if ( DragObject is null && (DragInstallTask?.IsCompleted ?? true) )
		{
			DragOffset = 0;

			if ( ev.Data.HasFileOrFolder )
			{
				var asset = AssetSystem.FindByPath( ev.Data.FileOrFolder );
				if ( asset is not null )
				{
					CreateDragObjectFromAsset( asset );
				}
			}

			if ( DragObject is null && ev.Data.Url is not null )
			{
				DragCancelSource?.Cancel();
				DragCancelSource = new CancellationTokenSource();
				DragInstallTask = InstallPackageAsync( ev.Data.Text, DragCancelSource.Token );
				return;
			}

			if ( DragObject is not null )
			{
				var b = DragObject.GetBounds();
				var offset = b.ClosestPoint( Vector3.Down * 10000 ) - DragObject.Transform.Position;
				DragOffset = offset.Length;
			}
		}

		//
		// Position the drag object
		//
		if ( DragObject is not null )
		{
			DragObject.Enabled = true;
			DragObject.Flags = GameObjectFlags.NotSaved | GameObjectFlags.Hidden;
			DragObject.Tags.Add( "isdragdrop" );



			var pos = tr.EndPosition + tr.Normal * DragOffset;

			DragObject.Transform.Position = pos;
			DragObject.Transform.Rotation = rot;
			return;
		}

	}

	void CreateDragObjectFromAsset( Asset asset )
	{
		asset.RecordOpened();

		//
		// A prefab asset!
		//
		if ( asset.LoadResource<PrefabFile>() is PrefabFile prefabFile )
		{
			DragObject = SceneUtility.Instantiate( prefabFile.Scene, Vector3.Zero, Rotation.Identity );
			return;
		}

		//
		// A model asset!
		//
		if ( asset.LoadResource<Model>() is Model modelAsset )
		{
			DragObject = SceneEditorSession.Active.Scene.CreateObject();
			DragObject.Name = modelAsset.ResourceName;

			var mc = DragObject.AddComponent<ModelComponent>();
			mc.Model = modelAsset;

		}
	}

	async Task InstallPackageAsync( string url, CancellationToken token )
	{
		var asset = await AssetSystem.InstallAsync( url, null, token );

		if ( token.IsCancellationRequested )
			return;

		if ( asset is not null )
		{
			CreateDragObjectFromAsset( asset );
		}
	}

	public override void OnDragLeave()
	{
		DragObject?.Destroy();
		DragObject = null;

		DragCancelSource?.Cancel();
		DragCancelSource = null;
	}
}
