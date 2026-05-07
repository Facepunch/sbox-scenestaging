using Voxels.Rendering;

namespace Sandbox;

public sealed class VoxelTest : Component, Component.ExecuteInEditor
{
	private SceneCubicVoxelsObject _sceneObject;

	[Property]
	public Vector3Int Size { get; set; } = 32;

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
		_sceneObject?.Generate( Size, Seed );
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
