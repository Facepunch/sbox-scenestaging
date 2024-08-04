namespace Sandbox;

/// <summary>
/// Emits particles in a line. The line can be flat or have a tube-like quality.
/// </summary>
[Title( "Model Emitter" )]
[Category( "Particles" )]
[Icon( "stacked_line_chart" )]
public sealed class ParticleModelEmitter : ParticleEmitter
{
	[Group( "Model" )]
	[Property] public GameObject Target { get; set; }

	[Group( "Placement" )]
	[Property] public bool OnEdge { get; set; }

	public Vector3 GetRandomPositionOnModel( ModelRenderer target )
	{
		if ( target is null )
			return Transform.Position;

		if ( target.Model.HitboxSet is not null && target.Model.HitboxSet.All.Count > 0 )
		{
			var boxIndex = Random.Shared.Int( 0, target.Model.HitboxSet.All.Count - 1 );
			var box = target.Model.HitboxSet.All[boxIndex];

			var tx = target.Transform.World;

			if ( target is SkinnedModelRenderer skinned )
			{
				skinned.TryGetBoneTransform( box.Bone, out tx );
			}

			return tx.PointToWorld( OnEdge ? box.RandomPointOnEdge : box.RandomPointInside );
		}

		if ( target.Model.Physics is not null )
		{
			return target.Transform.World.PointToWorld( OnEdge ? target.Model.PhysicsBounds.RandomPointOnEdge : target.Model.PhysicsBounds.RandomPointInside );
		}

		// Fallback to along bones?

		return target.Transform.World.PointToWorld( OnEdge ? target.Model.Bounds.RandomPointOnEdge : target.Model.Bounds.RandomPointInside );
	}

	public override bool Emit( ParticleEffect target )
	{

		var model = Target == null ? Components.GetInParentOrSelf<ModelRenderer>() : Target?.Components.Get<ModelRenderer>();
		if ( model is null ) return false;

		var targetPosition = GetRandomPositionOnModel( model );

		var p = target.Emit( targetPosition, Delta );

		return true;
	}
}
