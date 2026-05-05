using Sandbox.Utility;
using Voxels;

namespace Sandbox;

public sealed class VoxelTest : Component
{
	private byte[] _voxelArray;

	[Property]
	public Vector3Int Size { get; set; } = 8;

	[Button]
	public void Run()
	{
		_voxelArray = new byte[Size.x * Size.y * Size.z];

		var voxelSpan = new VoxelSpan<byte>( _voxelArray, Size );

		var field = Noise.SimplexField( new Noise.FractalParameters( Random.Shared.Next() ) );

		field.Sample( voxelSpan, BBox.FromPositionAndSize( 0f, 64f ), x => (byte)(x * 256f).Clamp( 0f, 255f ) );
	}

	protected override void DrawGizmos()
	{
		if ( _voxelArray is null ) return;

		var voxelSpan = new VoxelSpan<byte>( _voxelArray, Size );

		const float spacing = 32f;

		for ( var z = 0; z < voxelSpan.Size.z; z++ )
		{
			for ( var y = 0; y < voxelSpan.Size.y; y++ )
			{
				for ( var x = 0; x < voxelSpan.Size.x; x++ )
				{
					var pos = new Vector3( x, y, z ) * spacing;
					var radius = (voxelSpan[x, y, z] - 127.5f) / 128f * spacing * 2f;

					Gizmo.Draw.Color = radius < 0f ? Color.Blue : Color.Yellow;
					Gizmo.Draw.LineSphere( pos, Math.Abs( radius ) );
				}
			}
		}
	}
}
