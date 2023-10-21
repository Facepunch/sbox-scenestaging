using Sandbox;
using Sandbox.Utility;
using System.Linq;

public partial class Scene : GameObject
{
	FixedUpdate fixedUpdate = new FixedUpdate();
	public bool IsFixedUpdate;

	public float FixedDelta => fixedUpdate.Delta;

	/// <summary>
	/// How many times a second FixedUpdate runs
	/// </summary>
	public float FixedUpdateFrequency { get; set; } = 50.0f;

	public float TimeScale { get; set; } = 1.0f;

	public bool ThreadedAnimation { get; set; } = false;

	/// <summary>
	/// The update loop will turn certain settings on
	/// Here we turn them to their defaults.
	/// </summary>
	void PreTickReset()
	{
		SceneWorld.GradientFog.Enabled = false;
	}

	float time;
	float delta;

	public void EditorTick()
	{
		ProcessDeletes();
		PreRender();
		DrawGizmos();
		PreTickReset();

		// Only tick here if we're an editor scene
		// The game will tick a game scene!
		if ( IsEditor )
		{
			Update();
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

			bool use_fixed_update = true;


			if ( !use_fixed_update )
			{
				FixedUpdate();
			}
			else
			{
				fixedUpdate.Frequency = FixedUpdateFrequency;
				fixedUpdate.MaxSteps = 1; // todo - this will make the game run slower right now

				IsFixedUpdate = true;
				fixedUpdate.Run( FixedUpdate );
				IsFixedUpdate = false;
			}

			PreTickReset();

			Update();

			if ( ThreadedAnimation )
			{
				using ( Sandbox.Utility.Superluminal.Scope( "Scene.AnimUpdate", Color.Cyan ) )
				{
					var animModel = GetComponents<AnimatedModelComponent>( true, true ).ToArray();
					Parallel.ForEach( animModel, x => x.UpdateInThread() );
				}
			}

			

			ProcessDeletes();
		}

		Time.Now = oldNow;
		Time.Delta = oldDelta;
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
