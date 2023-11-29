public partial class GameObject
{
	protected virtual void Update()
	{
		if ( !Enabled )
			return;

		DirtyTagsUpdate();

		Transform.Update( IsProxy );

		Components.ForEach( "Update", true, c => c.InternalUpdate() );
		ForEachChild( "Tick", true, x => x.Update() );
	}

	protected virtual void FixedUpdate()
	{
		Components.ForEach( "FixedUpdate", true, c => c.InternalFixedUpdate() );
		ForEachChild( "FixedUpdate", true, x => x.FixedUpdate() );
	}
}
