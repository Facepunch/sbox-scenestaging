using Sandbox;
using Sandbox.Citizen;

public sealed class HitReactionTest : Component, Component.IDamageable
{
	[Property] public CitizenAnimationHelper Citizen { get; set; }
	TimeSince TimeSinceDone = 1;
	protected override void OnUpdate()
	{
		if ( TimeSinceDone < 1f ) return;

		TimeSinceDone = 0;

		//Citizen.ProceduralHitReaction( new DamageInfo( 50, GameObject, GameObject, null ), 10f, GameObject.Transform.Rotation.Left * 100f );
	}

	public void OnDamage( in DamageInfo damage )
	{
		Citizen.ProceduralHitReaction( damage, 1f, Vector3.Forward * 100f );
	}
}
