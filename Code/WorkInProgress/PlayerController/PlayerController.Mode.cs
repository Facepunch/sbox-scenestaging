using Sandbox.Movement;
namespace Sandbox;


public sealed partial class PlayerController : Component
{
	public MoveMode Mode { get; private set; }

	void ChooseBestMoveMode()
	{
		var best = GetComponents<MoveMode>( false ).OrderByDescending( x => x.Score( this ) ).First();
		if ( Mode == best ) return;

		Mode?.OnModeEnd( best );

		Mode = best;

		Body.PhysicsBody.Sleeping = false;

		Mode?.OnModeBegin();
	}
}
