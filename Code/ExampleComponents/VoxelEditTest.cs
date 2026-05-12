using Voxels.Rendering;

namespace Sandbox;

public sealed class VoxelEditTest : Component, Component.ExecuteInEditor
{
	[Property]
	public float Radius { get; set; } = 1024f;

	private Vector3 _prevPos;

	protected override void OnEnabled()
	{
		base.OnEnabled();

		_prevPos = WorldPosition;

		Transform.OnTransformChanged += Subtract;
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();

		Transform.OnTransformChanged -= Subtract;
	}

	[Button]
	public void Subtract()
	{
		if ( _prevPos.AlmostEqual( WorldPosition, 1f ) )
		{
			return;
		}

		Scene.Get<VoxelTest>().Subtract( new Capsule( _prevPos, WorldPosition, Radius ) );
		_prevPos = WorldPosition;
	}

	protected override void DrawGizmos()
	{
		Gizmo.Draw.Color = Color.Red;
		Gizmo.Draw.LineSphere( 0f, Radius );
	}
}
