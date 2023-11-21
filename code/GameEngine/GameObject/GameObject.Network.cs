using Sandbox;
using Sandbox.Network;

public partial class GameObject
{
	internal NetworkObject Net { get; private set; }

	public bool IsNetworkOwner => Net?.IsOwner ?? false;

	public bool IsProxy
	{
		get
		{
			if ( Net is not null )
			{
				return Net.IsProxy;
			}

			return Parent?.IsProxy ?? false;
		}
	}

	public bool IsNetworked => Net is not null;

	public float LastTx { get; set; }
	public float LastRcv { get; set; }

	bool _networked;

	public bool Networked
	{
		get
		{
			if ( Scene is null ) return false;

			if ( Scene.IsEditor )
			{
				return _networked;
			}

			return Net is not null;
		}
		set
		{
			if ( Scene is null ) return;

			if ( Scene.IsEditor )
			{
				_networked = value;
				return;
			}

			if ( value ) StartNetworking();
			else EndNetworking();
		}
	}

	/// <summary>
	/// Create this GameObject on the network. If you have permission to create an object then this
	/// object will be synced to all other clients.
	/// </summary>
	public void NetworkSpawn()
	{
		if ( Net is not null ) return;

		Net = new NetworkObject( this, true );
	}

	/// <summary>
	/// Initialize this object from the network
	/// </summary>
	internal void NetworkSpawnRemote( ObjectCreateMsg msg )
	{
		if ( Net is not null ) return;

		Net = new NetworkObject( this, msg );
	}

	/// <summary>
	/// Networking enabled from the editor
	/// </summary>
	void StartNetworking()
	{
		if ( Net is not null ) return;

		Net = new NetworkObject( this, false );
	}

	void EndNetworking()
	{
		if ( Net is null ) return;

		Net.Dispose();
		Net = null;
	}

	internal void NetworkUpdate()
	{
		var update = CreateNetworkUpdate();

		SceneNetworkSystem.Instance.Broadcast( update );

		LastTx = RealTime.Now;

	}


	internal ObjectUpdateMsg CreateNetworkUpdate()
	{
		var update = new ObjectUpdateMsg();
		update.Guid = Id;
		update.Transform = Transform.Local;
		update.Parent = Parent.Id;

		ByteStream data = ByteStream.Create( 32 );

		foreach ( var c in Components )
		{
			if ( c is INetworkBaby net )
			{
				ByteStream dataStream = ByteStream.Create( 32 );
				net.Write( ref dataStream );

				if ( dataStream.Length > 0 )
				{
					data.Write( c.GetType().Name ); // Ident of this component somehow
					data.Write( dataStream.Length ); // Ident of this component somehow
					data.Write( dataStream );
				}
			}
		}

		update.Data = Convert.ToBase64String( data.ToArray() );

		return update;
	}

	internal void Receive( ObjectUpdateMsg update )
	{
		float netRate = Scene.NetworkRate;
		LastRcv = RealTime.Now;

		Transform.FromNetwork( update.Transform, netRate );

		if ( string.IsNullOrWhiteSpace( update.Data ) )
			return;

		var data = Convert.FromBase64String( update.Data );

		ByteStream reader = ByteStream.CreateReader( data );

		while ( reader.ReadRemaining > 2 )
		{
			var t = reader.Read<string>();
			var l = reader.Read<int>();
			var componentData = reader.ReadByteStream( l );

			foreach ( var c in Components )
			{
				if ( c is not INetworkBaby net )
					continue;

				if ( c.GetType().Name != t )
					continue;

				net.Read( componentData );
			}
		}

	}
}

interface INetworkBaby
{
	void Write( ref ByteStream stream );
	void Read( ByteStream stream );
}
