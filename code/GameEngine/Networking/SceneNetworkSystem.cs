using Sandbox;
using Sandbox.Network;
using System.Text.Json.Nodes;

/// <summary>
/// This is created and referenced by the network system, as a way to route.
/// </summary>
public class SceneNetworkSystem : GameNetworkSystem
{
	internal static SceneNetworkSystem Instance { get; private set; }

	public SceneNetworkSystem()
	{
		Instance = this;

		AddHandler<ObjectCreateMsg>( OnObjectCreate );
		AddHandler<ObjectUpdateMsg>( OnObjectUpdate );
		AddHandler<ObjectDestroyMsg>( OnObjectDestroy );
		AddHandler<ObjectMessageMsg>( OnObjectMessage );
	}

	/// <summary>
	/// A client has joined and wants a snapshot of the world
	/// </summary>
	public override void GetSnapshot( Connection source, ref SnapshotMsg msg )
	{
		ThreadSafe.AssertIsMainThread();

		msg.Time = Time.Now;

		var o = new GameObject.SerializeOptions();
		o.SceneForNetwork = true;

		msg.SceneData = GameManager.ActiveScene.Serialize( o ).ToJsonString();
		GameManager.ActiveScene.SerializeNetworkObjects( msg.NetworkObjects );
	}


	public override void Dispose()
	{
		base.Dispose();

		if ( Instance == this )
			Instance = null;
	}

	protected override void Tick()
	{
		var sceneTitle = GameManager.ActiveScene?.Title;
		if ( string.IsNullOrWhiteSpace( sceneTitle ) ) sceneTitle = GameManager.ActiveScene.Name;
		if ( string.IsNullOrWhiteSpace( sceneTitle ) ) sceneTitle = "<empty>";

		MapName = sceneTitle;
	}

	/// <summary>
	/// We have recieved a snapshot of the world
	/// </summary>
	public override async Task SetSnapshotAsync( SnapshotMsg msg )
	{
		ThreadSafe.AssertIsMainThread();

		// TODO - really shouldn't allow set and get of ActiveScene
		// we should have a switchscene function, that disables everything
		// in the old scene.

		GameManager.ActiveScene?.Clear();
		GameManager.ActiveScene?.Destroy();
		GameManager.ActiveScene?.ProcessDeletes();

		GameManager.ActiveScene = new Scene();

		Time.Now = msg.Time;

		if ( !string.IsNullOrWhiteSpace( msg.SceneData ) )
		{
			var sceneData = JsonObject.Parse( msg.SceneData ).AsObject();
			GameManager.ActiveScene.Deserialize( sceneData );
		}

		foreach ( var nwo in msg.NetworkObjects )
		{
			if ( nwo is ObjectCreateMsg oc )
			{
				OnObjectCreate( oc, null );
				continue;
			}
		}

		GameManager.IsPlaying = true;
	}

	// TODO - system for registering global listeners like this

	public override void OnConnected( Connection client )
	{
		Log.Info( $"Client {client.Name} ({client.Id}) has joined the game!" );

		Action queue = default;

		foreach ( var c in GameManager.ActiveScene.GetComponents<BaseComponent.INetworkListener>( true, true ) )
		{
			queue += () => c.OnConnected( client );
		}

		queue?.Invoke();
	}

	public override void OnJoined( Connection client )
	{
		Action queue = default;

		foreach ( var c in GameManager.ActiveScene.GetComponents<BaseComponent.INetworkListener>( true, true ) )
		{
			queue += () => c.OnActive( client );
		}

		queue?.Invoke();
	}

	public override void OnLeave( Connection client )
	{
		Log.Info( $"Client {client.Name} ({client.Id}) has left the game!" );

		Action queue = default;

		foreach ( var c in GameManager.ActiveScene.GetComponents<BaseComponent.INetworkListener>( true, true ) )
		{
			queue += () => c.OnDisconnected( client );
		}

		queue?.Invoke();

		if ( client.Id == Guid.Empty )
			return;

		GameManager.ActiveScene.DestroyNetworkObjects( x => x.Owner == client.Id );
	}

	public override IDisposable Push()
	{
		if ( GameManager.ActiveScene is null )
			return null;

		return GameManager.ActiveScene.Push();
	}

	private void OnObjectCreate( ObjectCreateMsg message, Connection source )
	{
		// TODO: Does source have the authority to create?

		var go = new GameObject();

		// TODO: Does this server allow this client to be creating objects from json?
		go.Deserialize( JsonObject.Parse( message.JsonData ).AsObject() );
		go.NetworkSpawnRemote( message );

		//go.Receive( message.Update );
	}

	private void OnObjectUpdate( ObjectUpdateMsg message, Connection source )
	{
		var obj = GameManager.ActiveScene.Directory.FindByGuid( message.Guid );
		if ( obj is null )
		{
			Log.Warning( $"ObjectUpdate: Unknown object {message.Guid}" );
			return;
		}

		// TODO: Does source have the authority to update?

		obj.Receive( message );
	}

	private void OnObjectDestroy( ObjectDestroyMsg message, Connection source )
	{
		var obj = GameManager.ActiveScene.Directory.FindByGuid( message.Guid );
		if ( obj is null )
		{
			Log.Warning( $"ObjectDestroy: Unknown object {message.Guid}" );
			return;
		}

		// TODO: Does source have the authoruty to destroy?

		if ( obj.Net is not null )
		{
			obj.Net.OnNetworkDestroy();
		}
		else
		{
			obj.Destroy();
		}
	}

	private void OnObjectMessage( ObjectMessageMsg message, Connection source )
	{
		Rpc.HandleIncoming( message, source );
	}
}

public partial struct ObjectCreateMsg
{
	public string JsonData { get; set; }
	public Guid Guid { get; set; }
	public Guid Creator { get; set; }
	public Guid Owner { get; set; }
	public ObjectUpdateMsg Update { get; set; }
}

public partial struct ObjectUpdateMsg
{
	public Guid Guid { get; set; }
	public Transform Transform { get; set; }
	public Guid Parent { get; set; }
	public string Data { get; set; }
}

public partial struct ObjectDestroyMsg
{
	public Guid Guid { get; set; }

	// flags, reason, does it matter?
}

public partial struct ObjectMessageMsg
{
	public Guid Guid { get; set; }
	public string Component { get; set; } // todo - id the components better, allow for multiple
	public string MessageName { get; set; } // todo - this can be a hash for sure
	public object[] Arguments { get; set; }
}
