public sealed class DropObjectOnFootstep : Component
{
	[Property] GameObject Prefab { get; set; }
	[Property] SkinnedModelRenderer Source { get; set; }

	protected override void OnEnabled()
	{
		if ( !Source.IsValid() )
			return;

		Source.OnFootstepEvent += OnEvent;
	}

	protected override void OnDisabled()
	{
		if ( !Source.IsValid() )
			return;

		Source.OnFootstepEvent -= OnEvent;
	}

	private void OnEvent( SceneModel.FootstepEvent e )
	{
		if ( !Prefab.IsValid() )
			return;

		var tr = Scene.Trace
			.Ray( e.Transform.Position + Vector3.Up * 20, e.Transform.Position + Vector3.Up * -20 )
			.Run();

		if ( !tr.Hit )
			return;

		var angles = e.Transform.Rotation.Angles();
		angles.pitch = 0;
		angles.roll = 0;

		Prefab.Clone( new Transform( tr.HitPosition, angles.ToRotation() ) );
	}

}
