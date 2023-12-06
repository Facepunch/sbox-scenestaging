public partial class GameObject
{
	public GameTags Tags { get; init; }

	void DirtyTagsUpdate()
	{
		if ( !Tags.PopDirty() )
			return;

		Components.ForEach( "TagsUpdated", false, c => c.OnTagsUpdatedInternal() );
	}
}
