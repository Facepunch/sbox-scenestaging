using Sandbox;

public static class EditorScene
{
	public static Scene Active { get; set; }
	public static Scene[] All { get; set; }

	internal static Gizmo.Instance GizmoInstance { get; private set; }

	public static bool IsSelected( object obj ) => GizmoInstance.IsSelected( obj );
	public static void Select( object obj, bool clear = true )
	{
		GizmoInstance.SelectObject( obj, clear );
	}

	static void ClearSelection()
	{
		GizmoInstance.ClearSelection();
	}

	static EditorScene()
	{
		GizmoInstance = new Gizmo.Instance();

		Active = new Scene();
		Active.Name = "Untitled Scene";
		Active.IsEditor = true;

		All = new[] { Active };

		{
			var go = Active.CreateObject();
			go.Name = "Main Camera";
			go.Transform = new Transform( Vector3.Up * 100 + Vector3.Backward * 300 );
			go.AddComponent<CameraComponent>();
		}

		{
			var go = Active.CreateObject();
			go.Name = "Directional Light";
			go.Transform = new Transform( Vector3.Up * 200, Rotation.From( 80, 45, 0 ) );
			go.AddComponent<DirectionalLightComponent>();
		}

		{
			var go = Active.CreateObject();
			go.Name = "Sphere Object";
			var model = go.AddComponent<ModelComponent>();
			model.Model = Model.Load( "models/dev/sphere.vmdl" );
		}
	}

	public static Scene GetAppropriateScene()
	{
		if ( GameManager.IsPlaying ) return Scene.Active;
		return EditorScene.Active;
	}
}
