using Sandbox;

namespace Voxels.Modification;

public sealed record SphereModification( BrushOperation Operation, Sphere Sphere )
	: VoxelModification( 0x02, new BBox( Sphere.Center - Sphere.Radius, Sphere.Center + Sphere.Radius ) )
{
	protected override void OnWriteParameters( ParameterWriter writer )
	{
		writer.Write( (uint)Operation );
		writer.Write( Sphere.Center );
		writer.Write( Sphere.Radius );
	}
}

public sealed class SphereBrush : VoxelBrush
{
	[Property]
	public BrushOperation Operation
	{
		get;
		set
		{
			if ( value.Equals( field ) ) return;

			field = value;
			UpdateModification();
		}
	}

	[Property]
	public float Radius
	{ 
		get;
		set
		{
			if ( value.Equals( field ) ) return;

			field = value;
			UpdateModification();
		}
	} = 256f;

	protected override VoxelModification BuildModification()
	{
		return new SphereModification( Operation, new Sphere( WorldPosition, Radius ) );
	}

	protected override void DrawGizmos()
	{
		Gizmo.Draw.Color = Operation == BrushOperation.Add ? Color.Blue : Color.Yellow;
		Gizmo.Draw.LineSphere( new Sphere( 0f, Radius ) );
	}
}
