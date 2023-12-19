using Sandbox;
using System.Threading;

public sealed class NetworkTest : Component
{
	[Property] public GameObject ObjectToSpawn { get; set; }

	[Property] public GameObject HoldRelative { get; set; }

	GameObject Carrying;

	protected override void OnUpdate()
	{
		if ( IsProxy )
			return;

		var pc = Components.Get<PlayerController>();
		var lookDir = pc.EyeAngles.ToRotation();
		
		if ( Input.Pressed( "Attack1" ) )
		{
			var pos = Transform.Position + Vector3.Up * 40.0f + lookDir.Forward.WithZ( 0.0f ) * 50.0f;

			var o = SceneUtility.Instantiate( ObjectToSpawn, pos );
			o.Enabled = true;

			var p = o.Components.Get<Rigidbody>();
			p.Velocity = lookDir.Forward * 500.0f + Vector3.Up * 540.0f;

			o.Network.Spawn();
		}

		UpdatePickup();

	}

	protected override void OnPreRender()
	{
		if ( Carrying.IsValid() && !Carrying.IsProxy )
		{
			var offset = new Vector3( 0, 0, 40 );

			var pc = Components.Get<PlayerController>();

			Carrying.Transform.Position = HoldRelative.Transform.Position + HoldRelative.Parent.Transform.Rotation * offset;
			Carrying.Transform.Rotation = pc.Body.Transform.Rotation;
			Carrying.Components.Get<Rigidbody>().Velocity = 0;
			Carrying.Components.Get<Rigidbody>().AngularVelocity = 0;
		}
	}

	void UpdatePickup()
	{
		if ( Carrying.IsValid() )
		{
			if ( Carrying.IsProxy )
			{
				Drop();
				return;
			}

			var offset = new Vector3( 0, 0, 40 );

			var pc = Components.Get<PlayerController>();

			Carrying.Transform.Position = HoldRelative.Transform.Position + HoldRelative.Parent.Transform.Rotation * offset;
			Carrying.Transform.Rotation = pc.Body.Transform.Rotation;
			Carrying.Components.Get<Rigidbody>().Velocity = 0;
			Carrying.Components.Get<Rigidbody>().AngularVelocity = 0;
		}

		if ( Input.Pressed( "use" ) )
		{
			var pc = Components.Get<PlayerController>();
			var lookDir = pc.EyeAngles.ToRotation();

			if ( Carrying  is not null )
			{
				Carrying.Components.Get<Rigidbody>().Velocity = lookDir.Forward * 300.0f + Vector3.Up * 200.0f;
				Carrying.Components.Get<Rigidbody>().AngularVelocity = 0;

				Drop();
				return;
			}

			TryPickup();
		}
	}

	void TryPickup()
	{
		var pc = Components.Get<PlayerController>();
		var lookDir = pc.EyeAngles.ToRotation();
		var eyePos = Transform.Position + Vector3.Up * 60;

		var tr = Scene.Trace.WithoutTags( "player" ).Sphere( 16, eyePos, eyePos + lookDir.Forward * 100 ).Run();
		if ( !tr.Hit ) return;

		if ( tr.Body.GetGameObject() is not GameObject go )
			return;

		if ( !go.Tags.Has( "pickup" ) )
			return;

		go.Network.TakeOwnership();

		Carrying = go;
		Carrying.Tags.Add( "carrying" );

		var ca = Components.Get<Sandbox.Citizen.CitizenAnimationHelper>();

		ca.IkLeftHand = Carrying.Children.FirstOrDefault( x => x.Name == "hand_left" ) ?? Carrying;
		ca.IkRightHand = Carrying.Children.FirstOrDefault( x => x.Name == "hand_right" ) ?? Carrying;
	}

	void Drop()
	{
		if ( !Carrying.IsValid() )
			return;

		Carrying.Tags.Remove( "carrying" );
		Carrying.Network.DropOwnership();
		Carrying = null;

		var ca = Components.Get<Sandbox.Citizen.CitizenAnimationHelper>();
		ca.IkLeftHand = null;
		ca.IkRightHand = null;
	}
}
