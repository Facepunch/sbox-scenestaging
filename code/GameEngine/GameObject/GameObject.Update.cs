public partial class GameObject
{
	protected virtual void Update()
	{
		if ( !Enabled )
			return;

		ForEachComponent( "Update", true, c => c.InternalUpdate() );
		ForEachChild( "Tick", true, x => x.Update() );
	}

	protected virtual void FixedUpdate()
	{
		ForEachComponent( "FixedUpdate", true, c => c.FixedUpdate() );
		ForEachChild( "FixedUpdate", true, x => x.FixedUpdate() );
	}
}
