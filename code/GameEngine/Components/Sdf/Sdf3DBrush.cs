
using System.Threading.Tasks;
using Sandbox;
using Sandbox.Sdf;

[Title( "SDF 3D Brush" )]
public abstract class Sdf3DBrushComponent : BaseComponent, BaseComponent.ExecuteInEditor
{
	protected static Color AddColor { get; } = (Color)"#00a8e8";
	protected static Color SubtractColor { get; } = (Color)"#f0e000";

	[Property] public Sdf3DVolume Volume { get; set; }
	[Property] public Operator Operator { get; set; } = Operator.Add;

	private Modification<Sdf3DVolume, ISdf3D>? _nextModification;

	public Modification<Sdf3DVolume, ISdf3D> PrevModification { get; private set; }

	public Modification<Sdf3DVolume, ISdf3D> NextModification
	{
		get
		{
			if ( _nextModification != null )
			{
				return _nextModification.Value;
			}

			var sdf = OnBuildSdf();
			var world = GetComponentInParent<Sdf3DWorldComponent>();

			if ( world == null )
			{
				return default;
			}

			sdf = sdf.Transform( world.Transform.World.ToLocal( Transform.World ) );

			return (_nextModification = new Modification<Sdf3DVolume, ISdf3D>( sdf, Volume, Operator )).Value;
		}
	}

	private void InvalidateWorld()
	{
		_nextModification = null;
		GetComponentInParent<Sdf3DWorldComponent>()?.InvalidateBrush( this );
	}

	public override void OnEnabled()
	{
		InvalidateWorld();
		Transform.OnTransformChanged += Transform_Changed;
	}

	public override void OnDisabled()
	{
		InvalidateWorld();
		Transform.OnTransformChanged -= Transform_Changed;
	}

	private void Transform_Changed()
	{
		foreach ( var brush in GetComponents<Sdf3DBrushComponent>( true, true ) )
		{
			brush.InvalidateWorld();
		}
	}

	public override void OnValidate()
	{
		InvalidateWorld();
	}

	public void CommitModification()
	{
		PrevModification = NextModification;
	}

	public sealed override void DrawGizmos()
	{
		if ( !Gizmo.IsSelected && !Gizmo.IsChildSelected )
		{
			return;
		}

		Gizmo.Draw.Color = (Operator == Operator.Add ? AddColor : SubtractColor).WithAlpha( 0.5f );
		OnDrawGizmos();
	}

	protected virtual void OnDrawGizmos()
	{

	}

	protected abstract ISdf3D OnBuildSdf();
}

[Title( "Sphere Brush" )]
public class Sdf3DSphereBrushComponent : Sdf3DBrushComponent
{
	[Property] public float Radius { get; set; } = 128f;

	protected override void OnDrawGizmos()
	{
		Gizmo.Draw.LineSphere( new Sphere( 0, Radius ) );
	}

	protected override ISdf3D OnBuildSdf()
	{
		return new SphereSdf3D( Vector3.Zero, Radius );
	}
}

[Title( "Box Brush" )]
public class Sdf3DBoxBrushComponent : Sdf3DBrushComponent
{
	[Property] public Vector3 Size { get; set; } = new Vector3( 128f, 128f, 128f );
	[Property] public float CornerRadius { get; set; } = 32f;

	protected override void OnDrawGizmos()
	{
		Gizmo.Draw.LineBBox( new BBox( Size * -0.5f, Size * 0.5f ) );
	}

	protected override ISdf3D OnBuildSdf()
	{
		return new BoxSdf3D( Size * -0.5f, Size * 0.5f, CornerRadius );
	}
}
