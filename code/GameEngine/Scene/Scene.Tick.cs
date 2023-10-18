using Sandbox;
using Sandbox.Diagnostics;
using Sandbox.Utility;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class Scene : GameObject
{
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
			Tick();
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

			PreTickReset();

			Tick();

			ProcessDeletes();

			TickPhysics();

			ProcessDeletes();
		}
	}


	void TickPhysics()
	{
		PhysicsWorld.Gravity = Vector3.Down * 900;
		PhysicsWorld?.Step( Time.Delta );
		PostPhysics();
	}
}
