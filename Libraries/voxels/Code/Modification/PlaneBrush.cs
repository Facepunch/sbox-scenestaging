using Sandbox;

namespace Voxels.Modification;

public sealed class PlaneBrush : VoxelBrush
{
	public override uint ModificationTypeId => 0x01;

	[Property]
	public BrushOperation Operation { get; set; }

	protected override void OnWriteParameters( ParameterWriter writer )
	{
		writer.Write( (uint)Operation );
		writer.Write( WorldRotation.Up );
		writer.Write( Vector3.Dot( WorldRotation.Up, WorldPosition ) );
	}

	protected override void DrawGizmos()
	{
		if ( !Gizmo.IsSelected ) return;

		Gizmo.Draw.Color = Operation == BrushOperation.Add ? Color.Blue : Color.Yellow;
		Gizmo.Draw.LineCircle( 0f, Vector3.Up, 1024f );
		Gizmo.Draw.Arrow( 0f, Vector3.Up * 64f );
	}
}
