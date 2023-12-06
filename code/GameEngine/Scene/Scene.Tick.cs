using Sandbox;
using Sandbox.Utility;
using System.Linq;

public partial class Scene : GameObject
{
	FixedUpdate fixedUpdate = new FixedUpdate();
	public bool IsFixedUpdate { get; private set; }

	public float FixedDelta => fixedUpdate.Delta;

	/// <summary>
	/// How many times a second FixedUpdate runs
	/// </summary>
	[Property] public float FixedUpdateFrequency { get; set; } = 50.0f;

	/// <summary>
	/// If the frame took longer than a FixedUpdate step, we need to run multiple
	/// steps for that frame, to catch up. How many are allowed? Too few, and the 
	/// simluation will run slower than the game. If you allow an unlimited amount
	/// then the frame time could snowball to infinity and never catch up.
	/// </summary>
	[Property] public int MaxFixedUpdates { get; set; } = 5;

	[Property, Range( 0, 1 )] public float TimeScale { get; set; } = 1.0f;

	[Property] public bool ThreadedAnimation { get; set; } = true;

	/// <summary>
	/// If false, then instead of operating physics, and UpdateFixed in a fixed update frequency
	/// they will be called the same as Update - every frame, with a time delta.
	/// </summary>
	[Property] public bool UseFixedUpdate { get; set; } = true;

	/// <summary>
	/// The update loop will turn certain settings on
	/// Here we turn them to their defaults.
	/// </summary>
	void PreTickReset()
	{
		SceneWorld.GradientFog.Enabled = false;
	}

	float TimeNow = 0.0f;
	float TimeDelta = 0.1f;
	int Tick = 0;

	public void EditorTick()
	{
		TimeNow = RealTime.Now;
		TimeDelta = RealTime.Delta;
		using var timeScope = Time.Scope( TimeNow, TimeDelta, Tick );

		ProcessDeletes();
		PreRender();
		DrawGizmos();
		PreTickReset();
		PhysicsWorld.DebugDraw();

		// Only tick here if we're an editor scene
		// The game will tick a game scene!
		if ( IsEditor )
		{
			Update();
			Signal( GameObjectSystem.Stage.UpdateBones );
		}

		ProcessDeletes();
	}

	public void GameTick()
	{
		if ( Sandbox.Camera.Main is not null )
		{
			gizmoInstance.Input.Camera = Sandbox.Camera.Main;

			// default sound listener, it might get overriden anyway
			Sound.Listener = new( Sandbox.Camera.Main.Position, Sandbox.Camera.Main.Rotation );
		}

		TimeDelta = Time.Delta * TimeScale;
		TimeNow += TimeDelta;

		using var timeScope = Time.Scope( TimeNow, TimeDelta, Tick );

		using ( gizmoInstance.Push() )
		{
			ProcessDeletes();

			if ( GameManager.IsPaused )
				return;

			if ( !UseFixedUpdate )
			{
				FixedUpdate();
			}
			else
			{
				fixedUpdate.Frequency = FixedUpdateFrequency;
				fixedUpdate.MaxSteps = MaxFixedUpdates;

				IsFixedUpdate = true;
				fixedUpdate.Run( FixedUpdate );
				IsFixedUpdate = false;
			}

			PreTickReset();
			SceneNetworkUpdate();

			Update();
			Signal( GameObjectSystem.Stage.UpdateBones );

			ProcessDeletes();
		}
	}


	protected override void FixedUpdate()
	{
		Tick++;

		using ( Sandbox.Utility.Superluminal.Scope( "Scene.FixedUpdate", Color.Cyan ) )
		{
			Signal( GameObjectSystem.Stage.PhysicsStep );



			using ( Sandbox.Utility.Superluminal.Scope( "FixedUpdate", Color.Cyan ) )
			{
				base.FixedUpdate();
			}

			using ( Sandbox.Utility.Superluminal.Scope( "ProcessDeletes", Color.Cyan ) )
			{
				ProcessDeletes();
			}
		}
	}

}
