using Sandbox;
using Sandbox.Diagnostics;
using Sandbox.Services;
using System;
using System.Threading;

public sealed class DropObjectOnFootstep : Component
{
	[Property] GameObject Prefab { get; set; }
	[Property] SkinnedModelRenderer Source { get; set; }

	protected override void OnEnabled()
	{
		if ( Source is null )
			return;

		Source.OnFootstepEvent += OnEvent;
	}

	protected override void OnDisabled()
	{
		if ( Source is null )
			return;

		Source.OnFootstepEvent -= OnEvent;
	}

	private void OnEvent( SceneModel.FootstepEvent e )
	{
		var tr = Scene.Trace
			.Ray( e.Transform.Position + Vector3.Up * 20, e.Transform.Position + Vector3.Up * -20 )
			.Run();

		if ( !tr.Hit )
			return;

		var angles = e.Transform.Rotation.Angles();
		angles.pitch = 0;
		angles.roll = 0;

		SceneUtility.Instantiate( Prefab, new Transform( tr.HitPosition, angles.ToRotation() ) );
	}

}
