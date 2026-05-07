using System.Diagnostics;
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

	[Property]
	public int Seed { get; set; } = 12379162;

	[Button]
	public void RandomizeSeed()
	{
		Seed = Random.Shared.Next();
		Run();
	}

	[Button]
	public void Run()
	{
		_voxelArray = new byte[Size.x * Size.y * Size.z];

		var voxelSpan = new VoxelSpan<byte>( _voxelArray, Size );

		var field = Noise.SimplexField( new Noise.FractalParameters( Seed ) );
		var timer = Stopwatch.StartNew();

		var groundLevel = Size.z / 2;

		field.Sample( voxelSpan, BBox.FromPositionAndSize( 0f, 64f ),  (pos, value) => value > ((float)pos.z / Size.z).Clamp( 0.25f, 1f ) ? (byte)1 : (byte)0 );

		Log.Info( $"Generating: {timer.Elapsed.TotalMilliseconds:F2} ms" );

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
