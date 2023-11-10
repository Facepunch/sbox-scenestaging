
using System.Threading.Tasks;
using Sandbox;
using Sandbox.Sdf;
using Sandbox.Sdf.Noise;

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

			if ( !GameObject.IsValid )
			{
				return default;
			}

			var sdf = OnBuildSdf();
			var world = GetComponentInParent<Sdf3DWorldComponent>();

			var modifiers = GetComponents<Sdf3DModifierComponent>();

			foreach ( var modifier in modifiers )
			{
				sdf = modifier.Apply( sdf );
			}

			if ( world == null )
			{
				return default;
			}

			sdf = sdf.Transform( world.Transform.World.ToLocal( Transform.World ) );

			return (_nextModification = new Modification<Sdf3DVolume, ISdf3D>( sdf, Volume, Operator )).Value;
		}
	}

	internal void InvalidateWorld()
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

[Title( "Sphere Brush" ), Icon( "circle" )]
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

[Title( "Box Brush" ), Icon( "square ")]
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

public abstract class Sdf3DModifierComponent : BaseComponent, BaseComponent.ExecuteInEditor
{
	public abstract ISdf3D Apply( ISdf3D sdf );

	private void InvalidateWorld()
	{
		GetComponent<Sdf3DBrushComponent>()?.InvalidateWorld();
	}

	public override void OnValidate()
	{
		InvalidateWorld();
	}

	public override void OnEnabled()
	{
		InvalidateWorld();
	}

	public override void OnDisabled()
	{
		InvalidateWorld();
	}
}

[Title( "Noise Modifier" ), Icon( "waves" )]
public class Sdf3DNoiseComponent : Sdf3DModifierComponent
{
	[Property]
	public int Seed { get; set; } = 0x3680bf16;

	[Property]
	public Vector3 CellSize { get; set; } = new ( 256f, 256f, 256f );

	[Property]
	public float DistanceOffset { get; set; } = 0f;

	[Property]
	public float BiasScale { get; set; } = 0.125f;

	public override ISdf3D Apply( ISdf3D sdf )
	{
		return sdf.Bias( new CellularNoiseSdf3D( Seed, CellSize, DistanceOffset ), BiasScale );
	}
}
