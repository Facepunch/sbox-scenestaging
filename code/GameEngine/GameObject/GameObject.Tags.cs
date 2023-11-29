public partial class GameObject
{
	public GameTags Tags { get; init; }

	void DirtyTagsUpdate()
	{
		if ( !Tags.PopDirty() )
			return;

		ForEachComponent( "TagsUpdated", true, c => c.OnTagsUpdatedInternal() );
	}
}
