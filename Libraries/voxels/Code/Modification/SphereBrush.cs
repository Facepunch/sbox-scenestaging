using Sandbox;

namespace Voxels.Modification;

public sealed class SphereBrush : VoxelBrush
{
	public override uint ModificationTypeId => 0x01;

	[Property]
	public BrushOperation Operation { get; set; }

	[Property]
	public float Radius { get; set; } = 256f;

	public override BBox LocalBounds => new( mins: -Radius, maxs: Radius );

	protected override void OnWriteParameters( ParameterWriter writer )
	{
		writer.Write( (uint)Operation );
		writer.Write( WorldPosition );
		writer.Write( Radius );
	}

	protected override void DrawGizmos()
	{
		Gizmo.Draw.Color = Operation == BrushOperation.Add ? Color.Blue : Color.Yellow;
		Gizmo.Draw.LineSphere( new Sphere( 0f, Radius ) );
	}
}
