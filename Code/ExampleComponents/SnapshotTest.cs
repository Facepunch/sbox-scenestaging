using Sandbox;

public sealed class SnapshotTest : Component
{
	[Property]
	public int Counter { get; set; }

	[Property]
	public TextRenderer Text { get; set; }

	protected override void OnUpdate()
	{
		if ( Networking.IsHost )
		{
			Count( Counter + 1 );
		}
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	public void Count( int value )
	{
		if ( Counter != value - 1 )
		{
			Log.Warning( $"Jumping from {Counter} to {value - 1}!" );
		}

		Counter = value;
		Text.Text = value.ToString();
	}
}
