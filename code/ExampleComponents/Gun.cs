public sealed class Gun : Component
{
	[Property] public GameObject ObjectToSpawn { get; set; }

	protected override void OnUpdate()
	{
		if ( IsProxy )
			return;

		var pc = Components.GetInAncestors<PlayerController>();
		if ( pc is null )
			return;

		var lookDir = pc.EyeAngles.ToRotation();

		if ( Input.Pressed( "Attack1" ) )
		{
			var pos = Transform.Position + Vector3.Up * 40.0f + lookDir.Forward.WithZ( 0.0f ) * 50.0f;

			var o = ObjectToSpawn.Clone( pos );
			o.Enabled = true;

			var p = o.Components.Get<Rigidbody>();
			p.Velocity = lookDir.Forward * 500.0f + Vector3.Up * 540.0f;

			o.NetworkSpawn();
		}
	}
}
