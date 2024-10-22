using Sandbox.PhysicsCharacterMode;
namespace Sandbox;


public sealed partial class PhysicsCharacter : Component
{
	public BaseMode Mode { get; private set; }

	void ChooseBestMoveMode()
	{
		var best = GetComponents<BaseMode>( false ).OrderByDescending( x => x.Score( this ) ).First();
		if ( Mode == best ) return;

		Mode?.OnModeEnd( best );

		Mode = best;

		Body.PhysicsBody.Sleeping = false;

		Mode?.OnModeBegin();
	}
}
