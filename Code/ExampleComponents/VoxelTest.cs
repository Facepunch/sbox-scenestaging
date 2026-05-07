using Sandbox.Utility;
using Voxels;
using Voxels.Rendering;

namespace Sandbox;

public sealed class VoxelTest : Component, Component.ExecuteInEditor
{
	private byte[] _voxelArray;
	private SceneCubicVoxelsObject _sceneObject;

	[Property]
	public Vector3Int Size { get; set; } = 8;

	[Button]
	public void Run()
	{
		_voxelArray = new byte[Size.x * Size.y * Size.z];

		var voxelSpan = new VoxelSpan<byte>( _voxelArray, Size );

		var field = Noise.SimplexField( new Noise.FractalParameters( Random.Shared.Next() ) );

		field.Sample( voxelSpan, BBox.FromPositionAndSize( 0f, 64f ),  x => x < 0.5f ? (byte)0 : (byte)1 );

		_sceneObject?.SetVoxels( voxelSpan );
	}

	protected override void OnEnabled()
	{
		_sceneObject ??= new SceneCubicVoxelsObject( Scene.SceneWorld );
	}

	protected override void OnDisabled()
	{
		_sceneObject?.Delete();
		_sceneObject = null;
	}

	protected override void OnDestroy()
	{
		_sceneObject?.Delete();
		_sceneObject = null;
	}
}
