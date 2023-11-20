using Sandbox;

public sealed class NetworkTest : BaseComponent
{
	[Property] public GameObject ObjectToSpawn { get; set; }

	public override void Update()
	{
		if ( IsProxy )
			return;

		var pc = GetComponent<PlayerController>();
		var lookDir = pc.EyeAngles.ToRotation();
		
		if ( Input.Pressed( "Attack1" ) )
		{
			var pos = Transform.Position + Vector3.Up * 40.0f + lookDir.Forward.WithZ( 0.0f ) * 50.0f;

			var o = SceneUtility.Instantiate( ObjectToSpawn, pos );
			o.Enabled = true;

			var p = o.GetComponent<PhysicsComponent>();
			p.Velocity = lookDir.Forward * 500.0f + Vector3.Up * 540.0f;

			o.NetworkSpawn();
		}

	}
}
