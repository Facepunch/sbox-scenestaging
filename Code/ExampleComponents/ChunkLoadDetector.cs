using Sandbox;
using Voxels;

public sealed class ChunkLoadDetector : Component
{
	[RequireComponent]
	public VoxelVolume Voxels { get; init; }

	protected override void OnUpdate()
	{
		var player = Scene.GetAll<Sandbox.PlayerController>()
			.FirstOrDefault( x => !x.IsProxy );

		if ( player is null ) return;
		if ( player.GetComponent<Rigidbody>( includeDisabled: true ) is not { } body ) return;

		var bounds = new BBox( player.WorldPosition - 128f, player.WorldPosition + 128f );

		body.Enabled = Voxels.AreChunksLoaded( bounds );
	}
}
