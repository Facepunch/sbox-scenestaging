using Sandbox;
using System.Threading.Tasks;

public static class Program
{
	public static void Main()
	{
		var scene = new Scene();
		Scene.Active = scene;

		//  Camera.Main = new Camera();
		Camera.Main.World = Scene.Active.SceneWorld;
		Camera.Main.Position = 0;
		Camera.Main.ZNear = 1;
		Camera.Main.Tonemap.Enabled = true;
		Camera.Main.Tonemap.MinExposure = 0.1f;
		Camera.Main.Tonemap.MaxExposure = 2.0f;
		Camera.Main.Tonemap.Rate = 1.0f;
		Camera.Main.Tonemap.Fade = 1.0f;

		_ = LoadMapAsync();
	}

	static async Task LoadMapAsync()
	{
		var package = await Package.Fetch( "https://asset.party/facepunch/datacore", false );

		await package.MountAsync();

		var map = new Map( "maps/datacore", new SceneMapLoader( Scene.Active.SceneWorld, Scene.Active.PhysicsWorld ) );

		Scene.Active.NavigationMesh = new NavigationMesh();
		Scene.Active.NavigationMesh.Generate( Scene.Active.PhysicsWorld );
	}

	static Vector3 velocity;
	static Vector3 velocityDamp;
	static Angles viewAngles;

	[Event( "frame" )]
	public static void FramePofop()
	{
		if ( !GameManager.IsPlaying )
			return;

		Scene.Active.Tick();
		Scene.Active.PreRender();

		Vector3 move = 0;
		if ( Input.Down( "Forward" ) ) move += Vector3.Forward;
		if ( Input.Down( "Backward" ) ) move -= Vector3.Forward;
		if ( Input.Down( "Left" ) ) move += Vector3.Left;
		if ( Input.Down( "right" ) ) move += Vector3.Right;

		var speed = 310;
		if ( Input.Down( "run" ) ) speed = 5000;

		if ( Input.Pressed( "jump" ) )
		{
			Scene.Active.NavigationMesh.Generate( Scene.Active.PhysicsWorld );
		}

		if ( Input.Pressed( "attack1" ) )
		{
			// var model = new SceneModel(world, "models/rust_props/small_junk/apple.vmdl", Transform.Zero);
			//  model.Position = Camera.Main.Position + Camera.Main.Rotation.Forward * 40;

			var spawnTx = new Transform( Camera.Main.Position + Camera.Main.Rotation.Forward * 100 );

			var prefab = ResourceLibrary.Get<Prefab>( "ball.prefab" );
			var instance = PrefabSystem.Spawn( prefab, spawnTx );

			var phys = instance.GetComponent<PhysicsComponent>();
			phys.Velocity = Camera.Main.Rotation.Forward * 1000;
		}

		velocity += viewAngles.ToRotation() * move * RealTime.Delta * speed;
		velocity = velocity.ClampLength( 10000.0f );
		velocity = Vector3.SmoothDamp( velocity, 0, ref velocityDamp, 1.0f, RealTime.Delta );

		viewAngles += Input.AnalogLook;

		Camera.Main.Worlds.Add( Scene.Active.DebugSceneWorld );
		Camera.Main.Position += velocity * RealTime.Delta;
		Camera.Main.Rotation = viewAngles.ToRotation();
		Camera.Main.ZNear = 1;
		Camera.Main.ZFar = 50000;
		Camera.Main.FieldOfView = 90;
	}
}
