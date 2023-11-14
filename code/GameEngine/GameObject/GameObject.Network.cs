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
		var update = CreateNetworkUpdate();

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

	internal Net_ObjectUpdate CreateNetworkUpdate()
	{
		var update = new Net_ObjectUpdate();
		update.Guid = Id;
		update.Transform = Transform.Local;
		update.Parent = Parent.Id;

		ByteStream data = ByteStream.Create( 32 );

		foreach( var c in Components )
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

	internal void Receive( Net_ObjectUpdate update )
	{
		LastRcv = RealTime.Now;
		Transform.LerpTo( update.Transform, (1.0f / 30.0f) );

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
