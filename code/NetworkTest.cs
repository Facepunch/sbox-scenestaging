using Sandbox;

public sealed class NetworkTest : BaseComponent
{
	[Property] public GameObject ObjectToSpawn { get; set; }

	public override void Update()
	{
		if ( !GameObject.IsMine )
			return;

		if ( Input.Pressed( "Attack1" ) )
		{
			Log.Info( "ATTACK" );

			var o = SceneUtility.Instantiate( ObjectToSpawn, Transform.World );
			o.Enabled = true;

			NetworkObject.Instantiate( o );
		}
	}
}
