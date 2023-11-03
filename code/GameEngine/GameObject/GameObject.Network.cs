using Sandbox;
using static Sandbox.PhysicsContact;

public partial class GameObject
{
	internal NetworkObject Net { get; private set; }

	public bool IsMine => Net?.IsMine ?? false;

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
	}

	internal static void ObjectUpdate( NetworkUser user, Net_ObjectUpdate update )
	{
		var obj = GameManager.ActiveScene.Directory.FindByGuid( update.Guid );
		if ( obj is null )
		{
			Log.Warning( $"ObjectUpdate: Unknown object {update.Guid}" );
			return;
		}

		//obj.Transform.Local = update.Transform;
		obj.Transform.LerpTo( update.Transform, (1.0f / 30.0f) );
		
	}
}
