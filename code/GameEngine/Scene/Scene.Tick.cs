using Sandbox;
using Sandbox.Diagnostics;
using Sandbox.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

public partial class Scene : GameObject
{
	FixedUpdate fixedUpdate = new FixedUpdate();



	/// <summary>
	/// The update loop will turn certain settings on
	/// Here we turn them to their defaults.
	/// </summary>
	void PreTickReset()
	{
		SceneWorld.GradientFog.Enabled = false;
	}


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

		using ( gizmoInstance.Push() )
		{
			ProcessDeletes();

			if ( GameManager.IsPaused )
				return;

			bool noFixedUpdate = true;

			if ( noFixedUpdate )
			{
				FixedUpdate();
			}
			else
			{
				fixedUpdate.Frequency = 50;
				fixedUpdate.MaxSteps = 3;
				fixedUpdate.Run( FixedUpdate );
			}

			PreTickReset();

			Update();

			ProcessDeletes();



			ProcessDeletes();
		}
	}

	protected override void FixedUpdate()
	{
		var idealHz = 100.0f;
		var idealStep = 1.0f / idealHz;
		var steps = (Time.Delta / idealStep).FloorToInt().Clamp( 1, 10 );

		PhysicsWorld.Step( Time.Delta, steps );

		base.FixedUpdate();

		ProcessDeletes();
	}
}
