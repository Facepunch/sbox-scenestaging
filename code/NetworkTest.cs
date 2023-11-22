using Sandbox;
using System.Threading;

public sealed class NetworkTest : BaseComponent
{
	[Property] public GameObject ObjectToSpawn { get; set; }

	[Property] public GameObject HoldRelative { get; set; }

	GameObject Carrying;

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

		UpdatePickup();

	}

	void UpdatePickup()
	{
		if ( Carrying.IsValid() )
		{
			var pc = GetComponent<PlayerController>();

			Carrying.Transform.Position = HoldRelative.Transform.Position + HoldRelative.Transform.Rotation.Right * -30 + HoldRelative.Transform.Rotation.Forward * -30;
			Carrying.Transform.Rotation = pc.Body.Transform.Rotation;
			Carrying.GetComponent<PhysicsComponent>().Velocity = 0;
			Carrying.GetComponent<PhysicsComponent>().AngularVelocity = 0;
		}

		if ( Input.Pressed( "use" ) )
		{
			if ( Carrying  is not null )
			{
				Drop();
				return;
			}

			TryPickup();
		}
	}

	void TryPickup()
	{
		var pc = GetComponent<PlayerController>();
		var lookDir = pc.EyeAngles.ToRotation();
		var eyePos = Transform.Position + Vector3.Up * 60;

		var tr = Physics.Trace.WithoutTags( "player" ).Sphere( 16, eyePos, eyePos + lookDir.Forward * 100 ).Run();
		if ( !tr.Hit ) return;

		if ( tr.Body.GameObject is not GameObject go )
			return;

		if ( !go.Tags.Has( "pickup" ) )
			return;

		go.BecomeNetworkOwner();

		Carrying = go;
		Carrying.Tags.Add( "carrying" );



		Log.Info( $"Pick up {tr.Body.GameObject}" );
	}

	void Drop()
	{
		if ( !Carrying.IsValid() )
			return;

		Carrying.Tags.Remove( "carrying" );
		Carrying.Renounce();
		Carrying = null;
	}
}
