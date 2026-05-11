using Voxels.Rendering;

namespace Sandbox;

public sealed class VoxelEditTest : Component, Component.ExecuteInEditor
{
	[Property]
	public float Radius { get; set; } = 1024f;

	[Button]
	public void Subtract()
	{
		Scene.Get<VoxelTest>().Subtract( WorldPosition, Radius );
	}

	protected override void DrawGizmos()
	{
		Gizmo.Draw.Color = Color.Red;
		Gizmo.Draw.LineSphere( 0f, Radius );
	}
}
