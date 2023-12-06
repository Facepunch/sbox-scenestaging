public partial class GameObject
{
	protected virtual void Update()
	{
		if ( !Enabled )
			return;

		DirtyTagsUpdate();

		Transform.Update( IsProxy );

		Components.ForEach( "Update", false, c => c.InternalUpdate() );
		ForEachChild( "Tick", false, x => x.Update() );
	}

	protected virtual void FixedUpdate()
	{
		Components.ForEach( "FixedUpdate", false, c => c.InternalFixedUpdate() );
		ForEachChild( "FixedUpdate", false, x => x.FixedUpdate() );
	}
}
