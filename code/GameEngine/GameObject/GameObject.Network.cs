using Sandbox;
using Sandbox.Network;
using static Sandbox.PhysicsContact;

public partial class GameObject
{
	internal NetworkObject Net { get; private set; }

	public bool IsMine => IsNetworked && Net.IsMine;
	public bool IsProxy => IsNetworked && !IsMine;
	public bool IsNetworked => Net is not null;

	public float LastTx { get; set; }
	public float LastRcv { get; set; }

	internal void SetNetworkObject( NetworkObject obj )
	{
		Net = obj;
		Scene.RegisterNetworkedObject( this );
	}

	internal void ShutdownNetworking()
	{
		if ( Net is null )
			return;

		Scene.UnregisterNetworkObject( this );
	}

	internal void NetworkUpdate()
	{
		var update = new Net_ObjectUpdate();
		update.Guid = Id;
		update.Transform = Transform.Local;
		update.Parent = Parent.Id;

		SceneNetworkSystem.Instance.BroadcastJson( update );

		LastTx = RealTime.Now;

	}

	internal static void ObjectUpdate( NetworkChannel user, Net_ObjectUpdate update )
	{
		var obj = GameManager.ActiveScene.Directory.FindByGuid( update.Guid );
		if ( obj is null )
		{
			Log.Warning( $"ObjectUpdate: Unknown object {update.Guid}" );
			return;
		}

		obj.Receive( update );		
	}

	private void Receive( Net_ObjectUpdate update )
	{
		LastRcv = RealTime.Now;
		Transform.LerpTo( update.Transform, (1.0f / 30.0f) );
	}
}
