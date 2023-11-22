using Sandbox;
using Sandbox.Network;
using System.Reflection;
using static System.Runtime.InteropServices.JavaScript.JSType;

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

		using ByteStream data = ByteStream.Create( 32 );

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

	/// <summary>
	/// Stop being the network owner
	/// </summary>
	[Broadcast]
	public void Renounce()
	{
		if ( Net is null ) return;

		// TODO - check if we're allowed to do this
		// TODO - rules around this stuff

		if ( Net.Owner != Rpc.CallerId )
			return;

		Net.Owner = Guid.Empty;
	}

	/// <summary>
	/// Become the owner of this network object, if at all possible.
	/// </summary>
	public void BecomeNetworkOwner()
	{
		if ( Net is null ) return;

		Msg_BecomeNetworkOwner();
	}

	[Broadcast]
	void Msg_BecomeNetworkOwner()
	{
		// TODO - check if we're allowed to do this
		// TODO - rules around this stuff

		Net.Owner = Rpc.CallerId;
	}


	public void __rpc_Broadcast( Action resume, string methodName, params object[] argumentList )
	{
		if ( !Rpc.Calling && IsNetworked && SceneNetworkSystem.Instance is not null )
		{
			var msg = new ObjectMessageMsg();
			msg.Guid = Id;
			msg.Component = null;
			msg.MessageName = methodName;
			msg.ArgumentData = TypeLibrary.ToBytes( argumentList );

			SceneNetworkSystem.Instance.Broadcast( msg );
		}

		Rpc.PreCall();

		// we want to call this
		resume();
	}
}

interface INetworkBaby
{
	void Write( ref ByteStream stream );
	void Read( ByteStream stream );
}
