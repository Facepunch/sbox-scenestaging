using Sandbox;

interface INetworkSerializable
{
	void Write( ref ByteStream stream );
	void Read( ByteStream stream );
}
