/// <summary>
/// Swishes the player if the physics pressure exceeds a set amount
/// </summary>
public class PlayerSquish : Component, Component.ICollisionListener
{
	[Property] public GameObject Gibs { get; set; }

	protected override void OnFixedUpdate()
	{
		if ( IsProxy )
			return;

		UpdatePressure();
	}

	public void Explode()
	{
		var gibs = Gibs.Clone( WorldPosition );

		foreach ( var rb in gibs.GetComponentsInChildren<Rigidbody>() )
		{
			rb.Velocity = Vector3.Random * 600;
		}

		GameObject.Destroy();
	}


	float nominalPressure => 350;
	float pressure;
	int i = 0;

	void ICollisionListener.OnCollisionStart( Collision collision ) { }

	void ICollisionListener.OnCollisionUpdate( Collision collision )
	{
		if ( float.IsNaN( collision.Contact.Impulse ) ) return;

		var imp = collision.Contact.Impulse / collision.Self.Body.Mass;
		if ( imp < nominalPressure ) return;

		//DebugDrawSystem.Current.AddText( collision.Contact.Point + Vector3.Up * i * 16, $"[{imp:n0}] {collision.Other.Collider}" );
		//DebugDrawSystem.Current.AddText( collision.Contact.Point, $"{collision.Contact.Impulse}" ).WithTime( 10.0f );

		pressure += imp;
		i++;
	}

	void UpdatePressure()
	{
		i = 0;

		//if ( pressure > 100000 )
		//DebugDrawSystem.Current.AddText( WorldPosition + Vector3.Up * 80, $"pressure: {pressure}\nvelocity: {Controller.Body.Velocity}" );

		if ( pressure > 1000 )
		{
			Log.Info( $"Explode pressure: {pressure}" );
			Explode();
		}

		pressure -= 1000;
		if ( pressure < 0 ) pressure = 0;
	}
}
