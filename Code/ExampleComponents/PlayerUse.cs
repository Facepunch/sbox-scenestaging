public sealed class PlayerUse : Component
{
	CameraComponent camera { get; set; }

	[Property] SoundEvent useSound { get; set; }

	protected override void OnStart()
	{
		base.OnStart();

		if ( IsProxy ) return;

		camera = Scene.Camera;
	}

	protected override void OnUpdate()
	{
		if ( IsProxy ) return;

		if ( !camera.IsValid() ) return;

		if ( Input.Pressed( "use" ) )
		{
			Use();
		}
	}

	void Use()
	{
		var tr = Scene.Trace.Ray( camera.WorldPosition, camera.WorldPosition + camera.WorldRotation.Forward * 100 )
		.IgnoreGameObject( GameObject )
		.Run();

		if ( tr.Hit )
		{
			Sound.Play( useSound );

			if ( tr.GameObject.Components.Get<BaseInteractor>() is BaseInteractor interactable )
			{
				interactable.OnUsed();
			}
		}
	}
}
