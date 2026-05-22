using Sandbox;

namespace Voxels.Modification;

public sealed record PlaneModification( BrushOperation Operation, Plane Plane )
	: VoxelModification( 0x01 )
{
	protected override void OnWriteParameters( ParameterWriter writer )
	{
		writer.Write( (uint)Operation );
		writer.Write( Plane.Normal );
		writer.Write( Plane.Distance );
	}
}

public sealed class PlaneBrush : VoxelBrush
{
	[Property]
	public BrushOperation Operation { get; set; }

	protected override VoxelModification BuildModification()
	{
		return new PlaneModification( Operation, new Plane( WorldPosition, WorldRotation.Up ) );
	}

	protected override void DrawGizmos()
	{
		Gizmo.Draw.Color = Operation == BrushOperation.Add ? Color.Blue : Color.Yellow;
		Gizmo.Draw.LineCircle( 0f, Vector3.Up, 1024f );
		Gizmo.Draw.Arrow( 0f, Vector3.Up * 64f );
	}
}
