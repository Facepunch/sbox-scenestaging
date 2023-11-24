using Sandbox;
using Sandbox.Diagnostics;
using Sandbox.Network;

internal sealed class NetworkObject
{
	GameObject GameObject { get; set; }
	public Guid Creator { get; set; }
	public Guid Owner { get; set; }

	public bool IsOwner => Owner == Connection.Local.Id;
	public bool IsUnowned => Owner == Guid.Empty;
	public bool IsProxy
	{
		get
		{
			if ( IsOwner ) return false;
			if ( IsUnowned && GameNetworkSystem.IsHost ) return false;

			return true;
		}
	}

	bool hasNetworkDestroyed;

	internal NetworkObject( GameObject source )
	{
		GameObject = source;
		Creator = Guid.Empty;
		Owner = Guid.Empty;

		if ( !IsProxy )
		{
			SendInstantiateMessage();
		}

		GameObject.Scene.RegisterNetworkedObject( this );
	}

	internal NetworkObject( GameObject source, Connection owner )
	{
		Assert.NotNull( owner );

		GameObject = source;
		Creator = owner.Id;
		Owner = owner.Id;

		if ( !IsProxy )
		{
			SendInstantiateMessage();
		}

		GameObject.Scene.RegisterNetworkedObject( this );
	}

	internal NetworkObject( GameObject source, ObjectCreateMsg msg )
	{
		GameObject = source;

		Creator = msg.Creator;
		Owner = msg.Owner;

		GameObject.Scene.RegisterNetworkedObject( this );
	}

	internal void Dispose()
	{
		GameObject.Scene.UnregisterNetworkObject( this );
		GameObject = default;
	}

	void SendInstantiateMessage()
	{
		if ( SceneNetworkSystem.Instance is null )
			return;

		SceneNetworkSystem.Instance.Broadcast( GetCreateMessage( true ) );
	}

	internal void OnNetworkDestroy()
	{
		hasNetworkDestroyed = true;
		GameObject.Destroy();
	}

	internal void SendNetworkDestroy()
	{
		if ( hasNetworkDestroyed ) return;
		if ( IsProxy ) return;
		if ( SceneNetworkSystem.Instance is null ) return;

		var msg = new ObjectDestroyMsg();
		msg.Guid = GameObject.Id;

		SceneNetworkSystem.Instance.Broadcast( msg );
	}

	internal void NetworkUpdate()
	{
		if ( IsProxy ) return;

		GameObject.NetworkUpdate();
	}

	internal ObjectCreateMsg GetCreateMessage( bool includeUpdate )
	{
		var create = new ObjectCreateMsg();
		create.Guid = GameObject.Id;
		create.JsonData = GameObject.Serialize().ToJsonString();
		create.Owner = Owner;
		create.Creator = Creator;

		if ( includeUpdate )
		{
			create.Update = GameObject.CreateNetworkUpdate();
		}

		return create;
	}

	internal void Destroy()
	{
		GameObject.Destroy();
	}
}
