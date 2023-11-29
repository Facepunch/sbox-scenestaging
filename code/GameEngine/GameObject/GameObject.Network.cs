using Sandbox;

public partial class GameObject
{
	internal NetworkObject _net { get; private set; }

	public bool IsProxy => Network.IsProxy;

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

			return _net is not null;
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
	/// Initialize this object from the network
	/// </summary>
	internal void NetworkSpawnRemote( ObjectCreateMsg msg )
	{
		if ( _net is not null ) return;

		_net = new NetworkObject( this, msg );
	}

	/// <summary>
	/// Networking enabled from the editor
	/// </summary>
	void StartNetworking()
	{
		if ( _net is not null ) return;

		_net = new NetworkObject( this );
	}

	void EndNetworking()
	{
		if ( _net is null ) return;

		_net.Dispose();
		_net = null;
	}

	internal void NetworkUpdate()
	{
		if ( SceneNetworkSystem.Instance is null )
			return;

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

		using ByteStream data = ByteStream.Create( 32 );

		foreach ( var c in Components.GetAll() )
		{
			if ( c is INetworkSerializable net )
			{
				ByteStream dataStream = ByteStream.Create( 32 );
				net.Write( ref dataStream );

				if ( dataStream.Length > 0 )
				{
					data.Write( c.GetType().Name ); // Ident of this component somehow
					data.Write( dataStream.Length ); // Ident of this component somehow
					data.Write( dataStream );
				}

				dataStream.Dispose();
			}
		}

		update.Data = Convert.ToBase64String( data.ToArray() );

		return update;
	}

	internal void Receive( ObjectUpdateMsg update )
	{
		float netRate = Scene.NetworkRate;
		LastRcv = RealTime.Now;

		Transform.FromNetwork( update.Transform, netRate * 2.0f );

		if ( string.IsNullOrWhiteSpace( update.Data ) )
			return;

		var data = Convert.FromBase64String( update.Data );

		using ByteStream reader = ByteStream.CreateReader( data );

		while ( reader.ReadRemaining > 2 )
		{
			var t = reader.Read<string>();
			var l = reader.Read<int>();
			var componentData = reader.ReadByteStream( l );

			foreach ( var c in Components.GetAll() )
			{
				if ( c is not INetworkSerializable net )
					continue;

				if ( c.GetType().Name != t )
					continue;

				net.Read( componentData );
			}
		}

	}

	/// <summary>
	/// Stop being the network owner
	/// </summary>
	[Broadcast]
	void Msg_DropOwnership()
	{
		if ( _net is null ) return;

		// TODO - check if we're allowed to do this
		// TODO - rules around this stuff

		if ( _net.Owner != Rpc.CallerId )
			return;

		_net.Owner = Guid.Empty;
	}


	[Broadcast]
	void Msg_TakeOwnership()
	{
		// TODO - check if we're allowed to do this
		// TODO - rules around this stuff

		_net.Owner = Rpc.CallerId;
	}

	[Broadcast]
	void Msg_AssignOwnership( Guid guid )
	{
		// TODO - check if we're allowed to do this
		// TODO - rules around this stuff

		_net.Owner = guid;
	}


	public void __rpc_Broadcast( Action resume, string methodName, params object[] argumentList )
	{
		if ( !Rpc.Calling && Network.Active && SceneNetworkSystem.Instance is not null )
		{
			var msg = new ObjectMessageMsg();
			msg.Guid = Id;
			msg.Component = null;
			msg.MessageName = methodName;
			msg.Arguments = argumentList;

			SceneNetworkSystem.Instance.Broadcast( msg );
		}

		Rpc.PreCall();

		// we want to call this
		resume();
	}

	GameObject FindNetworkRoot()
	{
		if ( _net is not null ) return this;
		if ( Parent is null || Parent is Scene ) return null;

		return Parent.FindNetworkRoot();
	}

	/// <summary>
	/// Access network information for this GameObject
	/// </summary>
	public NetworkAccessor Network
	{
		get
		{
			// Find networking, even if it's in a parent
			if ( FindNetworkRoot() is GameObject networkRoot ) return new NetworkAccessor( networkRoot );

			// no networking, return for this, so Spawn etc can be called
			return new NetworkAccessor( this );
		}
	}

	public readonly ref struct NetworkAccessor
	{
		readonly GameObject go;

		public NetworkAccessor( GameObject o )
		{
			go = o;
		}

		/// <summary>
		/// Is this object networked
		/// </summary>
		public readonly bool Active => go._net is not null;

		/// <summary>
		/// Are we the creator of this network object
		/// </summary>
		public readonly bool IsOwner => OwnerId == Connection.Local.Id;

		/// <summary>
		/// The Id of the owner of this object
		/// </summary>
		public readonly Guid OwnerId => go._net?.Owner ?? Guid.Empty;

		/// <summary>
		/// Are we the creator of this network object
		/// </summary>
		public readonly bool IsCreator => CreatorId == Connection.Local.Id;

		/// <summary>
		/// The Id of the create of this object
		/// </summary>
		public readonly Guid CreatorId => go._net?.Creator ?? Guid.Empty;

		/// <summary>
		/// Is this object a network proxy. A network proxy is a network object that is not being simulated on the local pc.
		/// This means it's either owned by no-one and is being simulated by the host, or owned by another client.
		/// </summary>
		public readonly bool IsProxy => go._net?.IsProxy ?? false;

		/// <summary>
		/// Become the network owner of this object.
		/// </summary>
		public readonly bool TakeOwnership()
		{
			if ( !Active ) return false;

			if ( IsOwner ) return false;

			go.Msg_TakeOwnership();
			return true;
		}

		/// <summary>
		/// Set the owner of this object
		/// </summary>
		public readonly bool AssignOwnership( Connection channel )
		{
			if ( !Active ) return false;

			go.Msg_AssignOwnership( channel.Id );
			return true;
		}

		/// <summary>
		/// Stop being the owner of this object. Will clear the owner so the object becomes
		/// controlled by the server, and owned by no-one.
		/// </summary>
		public readonly bool DropOwnership()
		{
			if ( !Active ) return false;
			if ( !IsOwner ) return false;

			go.NetworkUpdate(); // send final update
			go.Msg_DropOwnership();
			return true;
		}

		/// <summary>
		/// Spawn on the network. If you have permission to spawn entities, this will spawn on
		/// everyone else's clients and you will be the owner.
		/// </summary>
		public readonly bool Spawn()
		{
			if ( Active ) return false;

			go.Enabled = true;
			go._net = new NetworkObject( go, Connection.Local );
			return true;
		}

		/// <summary>
		/// Spawn on the network. If you have permission to spawn entities, this will spawn on
		/// everyone else's clients and you will be the owner.
		/// </summary>
		public readonly bool Spawn( Connection owner )
		{
			if ( Active ) return false;

			// TODO - can we create objects for this owner

			go.Enabled = true;
			go._net = new NetworkObject( go );
			go.Network.AssignOwnership( owner );
			return true;
		}
	}

}
