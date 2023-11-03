public partial class GameObject
{
	internal NetworkObject Net { get; private set; }

	public bool IsMine => Net?.IsMine ?? false;

	internal void SetNetworkObject( NetworkObject obj )
	{
		Net = obj;
	}
}
