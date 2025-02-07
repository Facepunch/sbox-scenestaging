using Sandbox.Sdf;

#nullable enable

public sealed class Sdf3DWorldExample : Component
{
	[RequireComponent] public Sdf3DWorld World { get; private set; } = null!;

	[Property] public Sdf3DVolume? Volume { get; private set; }

	protected override void OnStart()
	{
		_ = BuildWorldAsync();
	}

	public async Task BuildWorldAsync()
	{
		await World.ClearAsync();

		if ( Volume is null ) return;

		await World.AddAsync( new SphereSdf3D( 0f, 512f ), Volume );

		for ( var i = 0; i < 100; ++i )
		{
			var radius = Random.Shared.Float( 64f, 128f );
			var pos = Random.Shared.VectorInSphere( 512f + radius );

			await World.SubtractAsync( new SphereSdf3D( pos, radius ) );
		}
	}
}
