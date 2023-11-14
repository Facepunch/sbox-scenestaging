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

	float time;

	public void EditorTick()
	{
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
			UpdateAnimationThreaded();
		}

		ProcessDeletes();
	}

	public void GameTick()
	{
		gizmoInstance.Input.Camera = Sandbox.Camera.Main;

		// Todo - make a scoping class to encompass this shit
		var delta = Time.Delta * TimeScale;
		time += delta;
		var oldNow = Time.Now;
		var oldDelta = Time.Delta;
		Time.Now = time;
		Time.Delta = delta;

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

			Update();
			UpdateAnimationThreaded();

			ProcessDeletes();
		}

		Time.Now = oldNow;
		Time.Delta = oldDelta;
	}

	void UpdateAnimationThreaded()
	{
		if ( !ThreadedAnimation )
			return;

		// TODO - faster way to accumulate these
		var animModel = GetComponents<AnimatedModelComponent>( true, true ).ToArray();

		//
		// Run the updates and the bone merges in a thread
		//
		using ( Sandbox.Utility.Superluminal.Scope( "Scene.AnimUpdate", Color.Cyan ) )
		{
			Parallel.ForEach( animModel, x => x.UpdateInThread() );
		}

		//
		// Run events in the main thread
		//
		using ( Sandbox.Utility.Superluminal.Scope( "Scene.AnimPostUpdate", Color.Yellow ) )
		{
			foreach( var x in animModel )
			{
				x.PostAnimationUpdate();
			}
		}
	}

	protected override void FixedUpdate()
	{
		using ( Sandbox.Utility.Superluminal.Scope( "Scene.FixedUpdate", Color.Cyan ) )
		{
			var idealHz = 220.0f;
			var idealStep = 1.0f / idealHz;
			int steps = (Time.Delta / idealStep).FloorToInt().Clamp( 1, 10 );

			using ( Sandbox.Utility.Superluminal.Scope( "PhysicsWorld.Step", Color.Cyan ) )
			{
				PhysicsWorld.Step( Time.Delta, steps );
			}

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
